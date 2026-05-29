using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppFishNet;
using Il2CppInterop.Runtime;
using Il2CppEpic.OnlineServices;
using Il2CppEpic.OnlineServices.Lobby;
using Il2CppPlayEveryWare.EpicOnlineServices.Samples;
using Il2CppTMPro;
using Il2Cpp_Scripts.Managers;
using Il2Cpp_Scripts.UI.Pre_Game;
using MelonLoader;
using LobbyKit.Features.Lobby;
using UnityEngine;
using EosLobby = Il2CppPlayEveryWare.EpicOnlineServices.Samples.Lobby;

namespace LobbyKit.Patches
{
    [HarmonyPatch]
    public static class LobbyPatches
    {
        private static bool _lastProximityChatEnabled = false;
        private static string _lastPlatform = string.Empty;
        private static string _lastRegion = string.Empty;
        private static bool _lastCrossplayEnabled = true;
        private static bool _hasLastLobbyParams = false;

        private static int _pendingAutoRestartAttempts = 0;
        private const int MaxAutoRestartAttempts = 3;
        private const int AutoRestartRetryDelayFrames = 300; // 5 seconds

        private static bool _awaitingLobbyLeft = false;
        private static bool _createCompleteFired = false;
        private static bool _pendingFishNetStop = false;

        [HarmonyPatch(typeof(LobbyManager), "CreateLobby")]
        [HarmonyPrefix]
        private static void LobbyManager_CreateLobby_Prefix(
            ref string lobbyName,
            ref int maxPlayers,
            ref bool isPublic,
            ref bool proximityChatEnabled,
            ref bool passwordProtected,
            ref string password,
            ref bool peacefulMode,
            ref string platform,
            ref string region,
            ref bool crossplayEnabled)
        {
            _lastProximityChatEnabled = proximityChatEnabled;
            _lastPlatform = platform ?? string.Empty;
            _lastRegion = region ?? string.Empty;
            _lastCrossplayEnabled = crossplayEnabled;
            _hasLastLobbyParams = true;

            LobbyUiController.Instance.ApplyLobbyManagerCreateLobby(ref lobbyName, ref maxPlayers);
        }

        [HarmonyPatch(typeof(UiReferenceController), "LeaveGame")]
        [HarmonyPrefix]
        private static void UiReferenceController_LeaveGame_Prefix()
        {
            LobbyKitCore.WasHosting = false;
        }

        [HarmonyPatch(typeof(UiReferenceController), "ReturnToMainMenu")]
        [HarmonyPrefix]
        private static void UiReferenceController_ReturnToMainMenu_Prefix(int messageType)
        {
            if (!LobbyKitCore.AutoRestartOnCrash || !LobbyKitCore.WasHosting || messageType == 0)
                return;

            LobbyKitCore.WasHosting = false;

            if (!_hasLastLobbyParams)
            {
                MelonLogger.Warning("[LobbyKit] Auto-restart: no prior lobby params recorded, skipping restart.");
                return;
            }

            _pendingAutoRestartAttempts = MaxAutoRestartAttempts;
            MelonCoroutines.Start(AutoRestartLobbyCoroutine());
        }

        [HarmonyPatch(typeof(LobbyManager), "OnCreateLobbyComplete")]
        [HarmonyPrefix]
        private static bool LobbyManager_OnCreateLobbyComplete_Prefix(int result)
        {
            // On success during auto-restart: stop any ghost FishNet server synchronously
            // before the original method calls serverMgr.StartConnection(). The ghost
            // server (recreated by Dissonance after the crash) may have set FishNet's
            // internal _started flag even if IsAnyServerStarted() returns false, causing
            // StartConnection to bail with "Server is already running."
            if (result == 0 && _pendingFishNetStop)
            {
                _pendingFishNetStop = false;
                try { InstanceFinder.ServerManager?.StopConnection(false); } catch { }
                try { InstanceFinder.ClientManager?.StopConnection(); } catch { }
            }

            if (result == 0 || _pendingAutoRestartAttempts <= 0)
            {
                _pendingAutoRestartAttempts = 0;
                _createCompleteFired = false;
                return true;
            }

            // Guard: EOSLobbyManager can fire this callback twice for one failure
            // (duplicate registration survives scene transitions). Only handle the first.
            if (_createCompleteFired)
                return false;
            _createCompleteFired = true;

            _pendingAutoRestartAttempts--;
            int attempt = MaxAutoRestartAttempts - _pendingAutoRestartAttempts;
            MelonLogger.Warning($"[LobbyKit] Auto-restart: lobby creation failed (attempt {attempt}/{MaxAutoRestartAttempts}), retrying in 5 seconds...");
            MelonCoroutines.Start(AutoRestartRetryCoroutine());
            return false;
        }

        [HarmonyPatch(typeof(UiReferenceController), "OnLobbyLeft")]
        [HarmonyFinalizer]
        private static Exception UiReferenceController_OnLobbyLeft_Finalizer(Exception __exception, int result)
        {
            if (!_awaitingLobbyLeft)
                return __exception;

            _awaitingLobbyLeft = false;

            if (__exception != null)
                MelonLogger.Warning("[LobbyKit] Auto-restart: OnLobbyLeft threw during FishNet teardown — continuing restart.");
            else if (result == 0)
                MelonLogger.Msg("[LobbyKit] Auto-restart: EOS lobby destroyed — creating new lobby...");
            else
                MelonLogger.Warning($"[LobbyKit] Auto-restart: EOS lobby destroy returned {result} — attempting create anyway...");

            MelonCoroutines.Start(AutoRestartCreateAfterLeaveCoroutine());
            return null; // suppress exception — EOS lobby is already destroyed
        }

        private static IEnumerator AutoRestartLobbyCoroutine()
        {
            MelonLogger.Msg("[LobbyKit] Lobby crashed — restarting lobby...");

            // Brief delay for the main menu to finish its UI transition
            for (int i = 0; i < 120; i++)
                yield return null;

            // Pre-stop FishNet before calling LeaveGame. In crash scenarios where
            // Dissonance recreates a ghost server, this primes FishNet's state so
            // OnLobbyLeft's StopConnection is more likely to complete cleanly.
            // Silently ignored if it throws (e.g., during test crashes with broken objects).
            try
            {
                var serverManager = InstanceFinder.ServerManager;
                if (serverManager != null && serverManager.IsAnyServerStarted())
                    serverManager.StopConnection(false);
                InstanceFinder.ClientManager?.StopConnection();
            }
            catch { }

            // Use the game's own leave path so the owner check runs and
            // DestroyCurrentLobby (not LeaveLobby) is called for the host.
            // LobbyManager.LeaveLobby() always calls LeaveLobby on EOSLobbyManager,
            // which EOS rejects for lobby owners — leaving the presence entry alive.
            var uiController = UiReferenceController.Instance;
            if (uiController != null)
            {
                MelonLogger.Msg("[LobbyKit] Auto-restart: destroying stale EOS lobby...");
                _awaitingLobbyLeft = true;
                uiController.LeaveGame(0);
                // CreateLobby is triggered from UiReferenceController_OnLobbyLeft_Finalizer
                // once EOS confirms the destroy completed (or if OnLobbyLeft throws).
            }
            else
            {
                MelonLogger.Warning("[LobbyKit] Auto-restart: UiReferenceController unavailable, skipping destroy.");
                InvokeCreateLobby(LobbyManager.Instance);
            }
        }

        private static IEnumerator AutoRestartCreateAfterLeaveCoroutine()
        {
            // Settle time after EOS confirms the lobby is destroyed
            for (int i = 0; i < 180; i++)
                yield return null;

            // StopConnection is async — poll until FishNet reports fully stopped.
            // Starting a new host before shutdown completes causes "Server failed to
            // start in host mode within timeout."
            int fishNetWait = 0;
            while (fishNetWait < 300)
            {
                bool stillRunning;
                try { stillRunning = InstanceFinder.ServerManager?.IsAnyServerStarted() == true; }
                catch { break; }
                if (!stillRunning) break;
                fishNetWait++;
                yield return null;
            }
            if (fishNetWait > 0)
                MelonLogger.Msg($"[LobbyKit] Auto-restart: waited {fishNetWait} extra frames for FishNet to stop.");

            InvokeCreateLobby(LobbyManager.Instance);
        }

        private static IEnumerator AutoRestartRetryCoroutine()
        {
            _createCompleteFired = false;
            for (int i = 0; i < AutoRestartRetryDelayFrames; i++)
                yield return null;

            InvokeCreateLobby(LobbyManager.Instance);
        }

        private static void InvokeCreateLobby(LobbyManager lobbyManager)
        {
            if (lobbyManager == null)
            {
                MelonLogger.Warning("[LobbyKit] Auto-restart: LobbyManager unavailable.");
                _pendingAutoRestartAttempts = 0;
                return;
            }

            _createCompleteFired = false;
            _pendingFishNetStop = true;

            string lobbyName = !string.IsNullOrWhiteSpace(LobbyKitCore.ServerName)
                ? LobbyKitCore.ServerName
                : $"{GameInfo.Instance?.PlayerName ?? "Host"}'s Lobby";

            MelonLogger.Msg("[LobbyKit] Auto-restart: creating lobby...");
            lobbyManager.CreateLobby(
                lobbyName,
                LobbyKitCore.ServerCapacity,
                LobbyKitCore.IsPublicLobby,
                _lastProximityChatEnabled,
                LobbyKitCore.IsPasswordProtected,
                LobbyKitCore.LobbyPassword,
                LobbyKitCore.IsPeacefulMode,
                _lastPlatform,
                _lastRegion,
                _lastCrossplayEnabled);
        }

        // Any NetworkObject whose stop callback throws (e.g. a TrinketPack with an
        // uninitialized SyncVar from a test-spawned object) will abort FishNet's despawn
        // loop, leaving the server in a corrupt state that prevents clean restart. Suppress
        // exceptions here so teardown continues past any broken NetworkObject.
        [HarmonyPatch(typeof(Il2CppFishNet.Object.NetworkObject), "InvokeStopCallbacks")]
        [HarmonyFinalizer]
        private static Exception NetworkObject_InvokeStopCallbacks_Finalizer(Exception __exception)
        {
            if (__exception != null)
                MelonLogger.Warning($"[LobbyKit] NetworkObject stop callback threw during teardown (suppressed): {__exception.GetType().Name}");
            return null;
        }

        [HarmonyPatch(typeof(EOSLobbyManager), "CreateLobby")]
        [HarmonyPrefix]
        private static void EOSLobbyManager_CreateLobby_Prefix(EOSLobbyManager __instance, EosLobby lobbyProperties)
        {
            LobbyUiController.Instance.ApplyEosCreateLobby(lobbyProperties);
        }

        [HarmonyPatch(typeof(LobbyManager), "SearchByAttributes")]
        [HarmonyPrefix]
        private static void LobbyManager_SearchByAttributes_Prefix(object[] __args)
        {
            if (__args == null || __args.Length == 0 || __args[0] == null)
                return;

            try
            {
                // We do nothing with this right now, but it's here if we ever decide to add more search parameters or add a minimum player count filter to the UI.
                object searchParameters = __args[0];
                LobbySearchSetParameterOptions option = BuildInt64(
                    LobbyInterface.SEARCH_MINCURRENTMEMBERS,
                    1,
                    ComparisonOp.Greaterthanorequal); 

                searchParameters.GetType().GetMethod("Add")?.Invoke(searchParameters, new object[] { option });
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"Failed to inject lobby search minimum-current-members parameter: {ex}");
            }
        }

        [HarmonyPatch(typeof(LobbyInterface), "CreateLobbySearch")]
        [HarmonyPrefix]
        private static void LobbyInterface_CreateLobbySearch_Prefix(ref CreateLobbySearchOptions options)
        {
            try
            {
                options.MaxResults = 100;
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"Failed to increase lobby search result limit: {ex}");
            }
        }

        [HarmonyPatch(typeof(UILobbyExplorer), "AddSearchParam")]
        [HarmonyPostfix]
        private static void UILobbyExplorer_AddSearchParam_Postfix(object[] __args)
        {
            if (__args == null || __args.Length < 4 || __args[0] == null)
                return;

            try
            {
                if (!IsMaxPlayersAttribute(__args[1]) || !IsEqualComparison(__args[3]) || !TryGetPositiveInt(__args[2], out int maxPlayers))
                    return;

                object searchParameters = __args[0];
                System.Type listType = searchParameters.GetType();
                System.Reflection.MethodInfo addMethod = listType.GetMethod("Add");
                System.Reflection.MethodInfo removeAtMethod = listType.GetMethod("RemoveAt");
                System.Reflection.PropertyInfo countProperty = listType.GetProperty("Count");
                int count = countProperty != null ? System.Convert.ToInt32(countProperty.GetValue(searchParameters)) : 0;
                if (addMethod == null || removeAtMethod == null || count == 0)
                    return;

                removeAtMethod.Invoke(searchParameters, new object[] { count - 1 });
                addMethod.Invoke(searchParameters, new object[] { BuildInt64((Utf8String)"MAXPLAYERS", maxPlayers, ComparisonOp.Greaterthanorequal) });
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"Failed to make UILobbyExplorer max-player search filter inclusive: {ex}");
            }
        }

        [HarmonyPatch(typeof(UILobbyExplorer), "ShowPanel")]
        [HarmonyPrefix]
        private static void UILobbyExplorer_ShowPanel_Prefix()
        {
            SearchLobbiesUiController.Instance.MarkLobbyExplorerOpening();
        }

        [HarmonyPatch(typeof(UILobbyExplorer), "GetListOfLobbies")]
        [HarmonyPrefix]
        private static void UILobbyExplorer_GetListOfLobbies_Prefix(UILobbyExplorer __instance)
        {
            SearchLobbiesUiController.Instance.ApplyPreferencesBeforeInitialSearch(__instance);
        }

        [HarmonyPatch(typeof(UILobbyExplorer), "UIUpdateSearchResults")]
        [HarmonyPostfix]
        private static void UILobbyExplorer_UIUpdateSearchResults_Postfix(UILobbyExplorer __instance)
        {
            try
            {
                SortVisibleLobbyRowsByCurrentPlayers(__instance);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"Failed to sort visible lobby rows by current players: {ex}");
            }
        }

        private static bool IsMaxPlayersAttribute(object value)
        {
            if (value == null)
                return false;

            string text = value.ToString();
            if (text == "MAXPLAYERS" || text == "9")
                return true;

            try
            {
                return System.Convert.ToInt32(value) == 9;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsEqualComparison(object value)
        {
            if (value is ComparisonOp comparisonOp)
                return comparisonOp == ComparisonOp.Equal;

            try
            {
                return System.Convert.ToInt32(value) == (int)ComparisonOp.Equal;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetPositiveInt(object value, out int result)
        {
            try
            {
                if (value is Il2CppSystem.Object il2CppObject)
                {
                    result = il2CppObject.Unbox<int>();
                    return result > 0;
                }

                result = System.Convert.ToInt32(value);
                return result > 0;
            }
            catch
            {
                result = 0;
                return false;
            }
        }

        private static void SortVisibleLobbyRowsByCurrentPlayers(UILobbyExplorer lobbyExplorer)
        {
            if (lobbyExplorer == null)
                return;

            var rows = new List<LobbyRowEntry>();
            foreach (UILobbyItem item in lobbyExplorer.GetComponentsInChildren<UILobbyItem>(true))
            {
                Transform itemTransform = item.transform;
                if (itemTransform?.parent == null || !TryGetDisplayedCurrentPlayers(item, out int currentPlayers))
                    continue;

                rows.Add(new LobbyRowEntry(itemTransform, currentPlayers, itemTransform.GetSiblingIndex()));
            }

            if (rows.Count < 2)
                return;

            rows.Sort((left, right) =>
            {
                int playerComparison = right.CurrentPlayers.CompareTo(left.CurrentPlayers);
                return playerComparison != 0 ? playerComparison : left.OriginalIndex.CompareTo(right.OriginalIndex);
            });

            for (int i = 0; i < rows.Count; i++)
                rows[i].Transform.SetSiblingIndex(i);
        }

        private static bool TryGetDisplayedCurrentPlayers(UILobbyItem item, out int currentPlayers)
        {
            foreach (TMP_Text text in item.GetComponentsInChildren<TMP_Text>(true))
            {
                string value = text?.text;
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                int slashIndex = value.IndexOf('/');
                if (slashIndex <= 0)
                    continue;

                if (int.TryParse(value.Substring(0, slashIndex).Trim(), out currentPlayers))
                    return true;
            }

            currentPlayers = 0;
            return false;
        }

        private sealed class LobbyRowEntry
        {
            public LobbyRowEntry(Transform transform, int currentPlayers, int originalIndex)
            {
                Transform = transform;
                CurrentPlayers = currentPlayers;
                OriginalIndex = originalIndex;
            }

            public Transform Transform { get; }
            public int CurrentPlayers { get; }
            public int OriginalIndex { get; }
        }

        [HarmonyPatch(typeof(UIMainMenu), "OnEnable")]
        [HarmonyPostfix]
        private static void UIMainMenu_OnEnable_Postfix(UIMainMenu __instance)
        {
            LobbyUiController.Instance.ConfigureMainMenuOnEnable(__instance);
            SearchLobbiesUiController.Instance.ConfigureMainMenuOnEnable(__instance);
        }

        private static LobbySearchSetParameterOptions BuildInt64(Utf8String key, long value, ComparisonOp comparisonOp)
        {
            var attributeValue = new AttributeDataValue();
            attributeValue.AsInt64 = new Il2CppSystem.Nullable<long>(value);

            var attribute = new AttributeData();
            attribute.Key = key;
            attribute.Value = attributeValue;

            var option = new LobbySearchSetParameterOptions();
            option.Parameter = new Il2CppSystem.Nullable<AttributeData>(attribute);
            option.ComparisonOp = comparisonOp;
            return option;
        }
    }
}

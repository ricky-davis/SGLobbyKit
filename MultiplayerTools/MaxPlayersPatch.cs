using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppEpic.OnlineServices.Lobby;
using Il2CppPlayEveryWare.EpicOnlineServices.Samples;
using Il2Cpp_Scripts.Managers;
using Il2Cpp_Scripts.UI.Pre_Game;
using UnityEngine;

namespace MultiplayerTools.Patches
{
    public class MultiplayerTools
    {
        public static MultiplayerTools Instance { get; private set; }
        public static dynamic maxPlayerSlider;
        public static Il2CppTMPro.TMP_InputField customLobbyNameInput;
        public static PlayerReference fakePlayerReference;

        private PlayerReferenceManager playerReferenceManager;
        private static bool referencesLoaded = false;

        public void SetReferences(PlayerReferenceManager prm)
        {
            playerReferenceManager = prm;
            referencesLoaded = true;
            fakePlayerReference = AddFakeServerPlayerReference();
        }

        public static void setMaxPlayers(int maxPlayers)
        {
            if (MultiplayerTools.maxPlayerSlider != null)
            {
                dynamic s = MultiplayerTools.maxPlayerSlider.slider;
                s.minValue = 1f;
                s.maxValue = (float)maxPlayers;
                s.wholeNumbers = true;

                if (s.value > 64f)
                    s.value = 64f;

                if (s.value < 1f)
                    s.value = 1f;

                MultiplayerTools.maxPlayerSlider.UpdateSliderValueDisplay();
            }
        }

        public static string PatchLobbyName()
        {
            if (MultiplayerTools.customLobbyNameInput != null)
            {
                var customName = MultiplayerTools.customLobbyNameInput.text;

                if (!string.IsNullOrWhiteSpace(customName))
                    return customName;
            }
            return GameInfo.Instance.PlayerName + "'s Lobby";
        }

        public static void BroadcastMessage(int clientId, string text)
        {
            fakePlayerReference = MultiplayerTools.AddFakeServerPlayerReference();
            var sm = Il2CppFishNet.InstanceFinder.ServerManager;

            if (sm == null)
            {
                Debug.LogError("ServerManager is null. Run this on host/server.");
                return;
            }

            var msg = new Il2Cpp_Scripts.Systems.Chat.ChatMessage
            {
                Username = "",
                UserProductId = fakePlayerReference.ProductUserId.ToString(),
                Message = text,
                MessageType = Il2Cpp_Scripts.Systems.Chat.ChatMessageType.Chat,
                SystemMessageType = (Il2Cpp_Scripts.Systems.Chat.SystemMessageType)(-1)
            };

            Debug.Log(
                "Sending msg Username=" + msg.Username +
                " UserProductId=" + msg.UserProductId +
                " Message=" + msg.Message
            );

            if (clientId < 0)
            {
                sm.Broadcast(msg, true);
                Debug.Log($"Broadcast chat to everyone: {text}");
                return;
            }

            if (!sm.Clients.TryGetValue(clientId, out Il2CppFishNet.Connection.NetworkConnection conn))
            {
                Debug.LogError($"No client found with id {clientId}");
                return;
            }

            sm.Broadcast(conn, msg, true);
            Debug.Log($"Sent private chat to client {clientId}: {text}");
        }

        public static PlayerReference AddFakeServerPlayerReference()
        {
            var manager = PlayerReferenceManager.Instance;

            if (manager == null || manager.sync_PlayerReferences == null)
            {
                UnityEngine.Debug.LogError("PlayerReferenceManager or sync_PlayerReferences is null.");
                return null;
            }

            if (manager.sync_PlayerReferences.Count == 0)
            {
                UnityEngine.Debug.LogError("No PlayerReferences to clone.");
                return null;
            }

            var src = manager.sync_PlayerReferences[0];

            string fakeProductId = "00000000000000000000000000000000";
            string fakeVoiceId = "";
            int fakeConnectionId = 32766;
            long fakePlatformUserId = 01234567898765432L;

            if (!manager._communicationPoliciesByPlatformUserId.ContainsKey(fakePlatformUserId))
            {
                fakePlayerReference = null;
                var srcPc = src.PlayerControl;
                var cloneGo = UnityEngine.Object.Instantiate(srcPc.gameObject);
                cloneGo.name = "Fake Server PlayerControl";
                cloneGo.SetActive(false);

                var fakePc = cloneGo.GetComponent<PlayerControl>();

                manager.Server_AddPlayerReference(
                    fakeProductId,
                    fakePlatformUserId,
                    fakeConnectionId,
                    "Server",
                    fakeVoiceId,
                    src.AuthPlatform,
                    fakePc
                );
            }
            else
            {
                Debug.Log("Fake server PlayerReference already exists, skipping creation.");
            }

            PlayerReference fake = null;

            for (int i = 0; i < manager.sync_PlayerReferences.Count; i++)
            {
                var pr = manager.sync_PlayerReferences[i];

                if (pr.ProductUserId == fakeProductId || pr.ConnectionID == fakeConnectionId)
                {
                    fake = pr;
                    break;
                }
            }

            if (!manager._communicationPoliciesByPlatformUserId.ContainsKey(fakePlatformUserId))
                manager.WarmCommunicationPolicy(fake, true);

            UnityEngine.Debug.Log("Added fake server PlayerReference: " + fake.ProductUserId);
            return fake;
        }
    }

    [HarmonyPatch(typeof(LobbyManager), "CreateLobby")]
    public static class LobbyManager_CreateLobby_SettingsPatch
    {
        private static void Prefix(
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
            lobbyName = MultiplayerTools.PatchLobbyName();
            maxPlayers = (int)MultiplayerTools.maxPlayerSlider.slider.value;
        }
    }

    [HarmonyPatch(typeof(EOSLobbyManager), "CreateLobby")]
    public static class EOSLobbyManager_CreateLobby_MaxPlayersPatch
    {
        private static void Prefix(EOSLobbyManager __instance, Lobby lobbyProperties)
        {
            lobbyProperties.MaxNumLobbyMembers = (uint)MultiplayerTools.maxPlayerSlider.slider.value;
        }
    }

    [HarmonyPatch(typeof(UICreateLobby), "Awake")]
    public static class UICreateLobby_Awake_MaxPlayersSliderPatch
    {
        private static void Postfix(UICreateLobby __instance)
        {
            if (__instance == null)
                return;

            dynamic mySlider = __instance.maxPlayersSlider;

            if (mySlider == null)
                return;
            MultiplayerTools.maxPlayerSlider = mySlider;

            dynamic s = mySlider.slider;

            if (s == null)
                return;

            MultiplayerTools.setMaxPlayers(64);

            Debug.Log($"customLobbyNameInput: {MultiplayerTools.customLobbyNameInput == null}");

            if (MultiplayerTools.customLobbyNameInput == null)
            {
                dynamic label = __instance.lobbyNameText;
                dynamic passwordInput = __instance.passwordInputField;
                var labelTransform = label.transform;
                var layoutParent = labelTransform.parent;

                var labelIndex = labelTransform.GetSiblingIndex();

                var cloneGO = UnityEngine.Object.Instantiate(
                    passwordInput.gameObject,
                    layoutParent
                );

                cloneGO.name = "CustomLobbyNameInput";

                var cloneTransform = cloneGO.transform;
                cloneTransform.SetSiblingIndex(labelIndex);

                var cloneInput = cloneGO.GetComponent<Il2CppTMPro.TMP_InputField>();
                var cloneRT = cloneGO.GetComponent<UnityEngine.RectTransform>();

                cloneRT.localScale = UnityEngine.Vector3.one;
                cloneRT.sizeDelta = new UnityEngine.Vector2(473.99f, 47.04f);

                cloneInput.text = "";
                cloneInput.characterLimit = 64;
                cloneInput.lineLimit = 1;
                cloneInput.contentType = Il2CppTMPro.TMP_InputField.ContentType.Standard;
                cloneInput.inputType = Il2CppTMPro.TMP_InputField.InputType.Standard;
                cloneInput.lineType = Il2CppTMPro.TMP_InputField.LineType.SingleLine;
                cloneInput.interactable = true;
                dynamic gameInfo = GameInfo.Instance;
                cloneInput.onValueChanged.AddListener(
                    (UnityEngine.Events.UnityAction<string>)((text) =>
                    {
                        label.text = text;
                    })
                );
                cloneInput.text = $"{gameInfo.PlayerName}'s Lobby";

                if (cloneInput.placeholder != null)
                {
                    var placeholderText = cloneInput.placeholder.GetComponent<Il2CppTMPro.TMP_Text>();
                    if (placeholderText != null)
                    {
                        var loc = placeholderText.GetComponent<UnityEngine.Localization.Components.LocalizeStringEvent>();
                        if (loc != null)
                            UnityEngine.Object.Destroy(loc);
                        placeholderText.text = "Lobby name...";
                    }
                }

                label.gameObject.SetActive(false);
                cloneGO.SetActive(true);

                MultiplayerTools.customLobbyNameInput = cloneInput;
            }
        }
    }
}

using System;
using System.Collections;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using MelonLoader.NativeUtils;
using UnityEngine;
using Object = UnityEngine.Object;

[assembly: MelonInfo(typeof(MultiplayerTools.MultiplayerToolsCore), "MultiplayerTools", "0.1.0", "Spyci")]

namespace MultiplayerTools
{
    public class MultiplayerToolsCore : MelonMod
    {
        public static MultiplayerToolsCore Instance;
        private PlayerReference localPlayer;
        private PlayerReference[] players = (PlayerReference[])(object)new PlayerReference[0];
        public static bool isHost = false;

        private static MelonPreferences_Category _preferences;
        private static MelonPreferences_Entry<bool> _enableGuestBangCommands;
        private static MelonPreferences_Entry<string> _serverName;
        private static MelonPreferences_Entry<int> _serverCapacity;
        private static MelonPreferences_Entry<bool> _isPublicLobby;
        private static MelonPreferences_Entry<bool> _isPasswordProtected;
        private static MelonPreferences_Entry<string> _lobbyPassword;
        private static MelonPreferences_Entry<bool> _isPeacefulMode;
        private static MelonPreferences_Entry<bool> _isTextChatOnly;

        public static bool EnableGuestBangCommands => _enableGuestBangCommands?.Value ?? true;
        public static string ServerName => _serverName?.Value ?? string.Empty;
        public static int ServerCapacity => _serverCapacity?.Value ?? 8;
        public static bool IsPublicLobby => _isPublicLobby?.Value ?? true;
        public static bool IsPasswordProtected => _isPasswordProtected?.Value ?? false;
        public static string LobbyPassword => _lobbyPassword?.Value ?? string.Empty;
        public static bool IsPeacefulMode => _isPeacefulMode?.Value ?? false;
        public static bool IsTextChatOnly => _isTextChatOnly?.Value ?? false;

        private PlayerReferenceManager _playerReferenceManager;

        public bool ReferencesLoaded = false;
        public void SavePrefs()
        {
            MelonPreferences.Save();
        }

        public override unsafe void OnInitializeMelon()
        {
            Instance = this;

            _preferences = MelonPreferences.CreateCategory("MultiplayerTools", "MultiplayerTools");
            _enableGuestBangCommands = _preferences.CreateEntry("EnableGuestBangCommands", true, "Enable Guest Bang Commands", "Allow non-host players to use custom bang chat commands like !tp.");
            _serverName = _preferences.CreateEntry("ServerName", string.Empty, "Server Name", "Custom default lobby/server name. Leave empty to use '<PlayerName>\'s Lobby'.");
            _serverCapacity = _preferences.CreateEntry("ServerCapacity", 8, "Server Capacity", "Saved default value for the max players slider.");
            _isPublicLobby = _preferences.CreateEntry("IsPublicLobby", true, "Public Lobby", "Saved default for public/private lobby.");
            _isPasswordProtected = _preferences.CreateEntry("IsPasswordProtected", false, "Password Protected", "Saved default for password protection.");
            _lobbyPassword = _preferences.CreateEntry("LobbyPassword", string.Empty, "Lobby Password", "Saved default lobby password.");
            _isPeacefulMode = _preferences.CreateEntry("IsPeacefulMode", false, "Peaceful Mode", "Saved default for peaceful mode.");
            _isTextChatOnly = _preferences.CreateEntry("IsTextChatOnly", false, "Text Chat Only", "Saved default for text-chat-only mode.");
            MelonPreferences.Save();

            HarmonyInstance.PatchAll();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            ReferencesLoaded = false;
            MelonCoroutines.Start(LoadReferences());
        }

        [HarmonyPatch(typeof(PlayerReferenceManager), "OnPlayerReferenceAdded")]
        public static class PlayerJoinPatch
        {
            private static void Postfix(PlayerReferenceManager __instance, int index)
            {
                PlayerReference val = __instance.GetPlayerReferences()[index];
                if (val.IsLocalPlayerInstance())
                {
                    Instance.localPlayer = val;
                    if (Utils.FindHostPlayer() == val.PlayerControl)
                    {
                        isHost = true;
                    }
                }
                Instance.PlayerJoinedGame(val);
            }
        }

        [HarmonyPatch(typeof(PlayerReferenceManager), "OnPlayerReferenceRemoved")]
        public static class PlayerLeavePatch
        {
            private static void Postfix(PlayerReferenceManager __instance, int index)
            {
                Instance.PlayerLeftGame(index);
            }
        }
        public void PlayerJoinedGame(PlayerReference p)
        {
            players.SetValue(p, players.Length);
            if (p.IsLocalPlayerInstance())
            {
                localPlayer = p;
            }
        }

        public void PlayerLeftGame(int index)
        {
            if (index >= 0 && index < players.Length)
            {
                if (players[index].IsLocalPlayerInstance())
                {
                    localPlayer = null;
                    isHost = false;
                }
                players[index] = null;
            }
        }

        public PlayerReference GetLocalPlayer()
        {
            if (localPlayer == null)
            {
                return null;
            }
            return localPlayer;
        }

        public static void SetEnableGuestBangCommands(bool value)
        {
            if (_enableGuestBangCommands == null)
                return;

            _enableGuestBangCommands.Value = value;
            MelonPreferences.Save();
        }

        public static void SetServerName(string value)
        {
            if (_serverName == null)
                return;

            _serverName.Value = value ?? string.Empty;
            MelonPreferences.Save();
        }

        public static void SetServerCapacity(int value)
        {
            if (_serverCapacity == null)
                return;

            _serverCapacity.Value = Math.Clamp(value, 1, 64);
            MelonPreferences.Save();
        }

        public static void SetIsPublicLobby(bool value)
        {
            if (_isPublicLobby == null)
                return;
            _isPublicLobby.Value = value;
            MelonPreferences.Save();
        }

        public static void SetIsPasswordProtected(bool value)
        {
            if (_isPasswordProtected == null)
                return;
            _isPasswordProtected.Value = value;
            MelonPreferences.Save();
        }

        public static void SetLobbyPassword(string value)
        {
            if (_lobbyPassword == null)
                return;
            _lobbyPassword.Value = value ?? string.Empty;
            MelonPreferences.Save();
        }

        public static void SetIsPeacefulMode(bool value)
        {
            if (_isPeacefulMode == null)
                return;
            _isPeacefulMode.Value = value;
            MelonPreferences.Save();
        }

        public static void SetIsTextChatOnly(bool value)
        {
            if (_isTextChatOnly == null)
                return;
            _isTextChatOnly.Value = value;
            MelonPreferences.Save();
        }

        private IEnumerator LoadReferences()
        {
            while ((Object)(object)_playerReferenceManager == (Object)null)
            {
                _playerReferenceManager = PlayerReferenceManager.Instance;
                yield return null;
            }
            List<StatueSetup> statueSetups = null;
            while (statueSetups == null || statueSetups.Count == 0)
            {
                statueSetups = ((IEnumerable<StatueSetup>)Object.FindObjectsByType<StatueSetup>((FindObjectsSortMode)0)).ToList();
                Debug.Log("[Core] Waiting for scene references to load...");
                Debug.Log("Count: " + statueSetups.Count);
                yield return null;
            }

            Object.FindObjectsByType<StatueSetup>((FindObjectsSortMode)0).ToList();
            ReferencesLoaded = true;
            Debug.Log("[Core] Scene references loaded successfully.");
        }
    }
}

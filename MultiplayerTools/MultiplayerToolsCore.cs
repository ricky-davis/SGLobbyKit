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
        private readonly List<PlayerReference> players = new List<PlayerReference>();
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
        private static MelonPreferences_Entry<string> _messageOfTheDay;
        private static MelonPreferences_Entry<bool> _showJoinMessages;
        private static MelonPreferences_Entry<bool> _showLeaveMessages;
        private static MelonPreferences_Entry<int> _joinMessageSize;
        private static MelonPreferences_Entry<int> _leaveMessageSize;

        public static bool EnableGuestBangCommands => _enableGuestBangCommands?.Value ?? true;
        public static string ServerName => _serverName?.Value ?? string.Empty;
        public static int ServerCapacity => _serverCapacity?.Value ?? 8;
        public static bool IsPublicLobby => _isPublicLobby?.Value ?? true;
        public static bool IsPasswordProtected => _isPasswordProtected?.Value ?? false;
        public static string LobbyPassword => _lobbyPassword?.Value ?? string.Empty;
        public static bool IsPeacefulMode => _isPeacefulMode?.Value ?? false;
        public static bool IsTextChatOnly => _isTextChatOnly?.Value ?? false;
        public static string MessageOfTheDay => _messageOfTheDay?.Value ?? string.Empty;
        public static bool ShowJoinMessages => _showJoinMessages?.Value ?? true;
        public static bool ShowLeaveMessages => _showLeaveMessages?.Value ?? true;
        public static int JoinMessageSize => _joinMessageSize?.Value ?? 75;
        public static int LeaveMessageSize => _leaveMessageSize?.Value ?? 75;

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
            _messageOfTheDay = _preferences.CreateEntry("MessageOfTheDay", string.Empty, "Message of the Day", "Private chat message sent to each player when they join your hosted lobby. Leave empty to disable.");
            _showJoinMessages = _preferences.CreateEntry("ShowJoinMessages", true, "Show Join Messages", "Broadcast a chat message when a player joins your hosted lobby.");
            _showLeaveMessages = _preferences.CreateEntry("ShowLeaveMessages", true, "Show Leave Messages", "Broadcast a chat message when a player leaves your hosted lobby.");
            _joinMessageSize = _preferences.CreateEntry("JoinMessageSize", 75, "Join Message Size", "Font size percentage for join messages (e.g. 75 for 75%).");
            _leaveMessageSize = _preferences.CreateEntry("LeaveMessageSize", 75, "Leave Message Size", "Font size percentage for leave messages (e.g. 75 for 75%).");
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
                var refs = __instance.GetPlayerReferences();

                if (refs == null || index < 0 || index >= refs.Count)
                    return;

                var val = refs[index];

                if (val == null)
                    return;

                if (val.IsLocalPlayerInstance())
                {
                    Instance.localPlayer = val;
                    isHost = val.ConnectionID == 32767;
                }

                Instance.PlayerJoinedGame(val);
            }
        }

        [HarmonyPatch(typeof(PlayerReferenceManager), "OnPlayerReferenceRemoved")]
        public static class PlayerLeavePatch
        {
            private static void Postfix(PlayerReferenceManager __instance, int index, PlayerReference removedItem)
            {
                Instance.PlayerLeftGame(removedItem);
            }
        }
        public void PlayerJoinedGame(PlayerReference p)
        {
            if (p == null)
                return;

            bool isLocalPlayer = p.IsLocalPlayerInstance();
            bool isNewConnection = !players.Any(player => player != null && player.ConnectionID == p.ConnectionID);
            if (isNewConnection)
                players.Add(p);

            if (isLocalPlayer)
            {
                localPlayer = p;
                isHost = p.ConnectionID == 32767;
            }
            else if (isHost && isNewConnection && ShowJoinMessages)
            {
                string username = Patches.ChatSystem.AutoCloseTmpRichText(string.IsNullOrWhiteSpace(p.Username) ? "A player" : p.Username);
                Patches.ChatSystem.BroadcastSystemMessage($"<size={JoinMessageSize}%><#FA0>{username} joined.");
            }

            if (isHost)
                Patches.ChatSystem.SendMotdToPlayer(p);
        }

        public void PlayerLeftGame(PlayerReference removedPlayer)
        {
            if (removedPlayer == null)
                return;

            bool wasTrackedPlayer = players.Any(player => player != null && player.ConnectionID == removedPlayer.ConnectionID);
            bool isLocalPlayer = removedPlayer.IsLocalPlayerInstance();
            if (isHost && wasTrackedPlayer && !isLocalPlayer && ShowLeaveMessages)
            {
                string username = Patches.ChatSystem.AutoCloseTmpRichText(string.IsNullOrWhiteSpace(removedPlayer.Username) ? "A player" : removedPlayer.Username);
                Patches.ChatSystem.BroadcastSystemMessage($"<size={LeaveMessageSize}%><#FA0>{username} left.");
            }

            players.RemoveAll(player => player == null || player.ConnectionID == removedPlayer.ConnectionID);
            Patches.ChatSystem.ForgetMotdRecipient(removedPlayer.ConnectionID);
            Patches.ChatSystem.ForgetTeleportRequests(removedPlayer.ConnectionID);

            if (localPlayer != null && localPlayer.ConnectionID == removedPlayer.ConnectionID)
            {
                localPlayer = null;
                isHost = false;
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

        public static void SetMessageOfTheDay(string value)
        {
            if (_messageOfTheDay == null)
                return;

            _messageOfTheDay.Value = value ?? string.Empty;
            MelonPreferences.Save();
        }

        public static void SetShowJoinMessages(bool value)
        {
            if (_showJoinMessages == null)
                return;

            _showJoinMessages.Value = value;
            MelonPreferences.Save();
        }

        public static void SetShowLeaveMessages(bool value)
        {
            if (_showLeaveMessages == null)
                return;

            _showLeaveMessages.Value = value;
            MelonPreferences.Save();
        }

        public static void SetJoinMessageSize(int value)
        {
            if (_joinMessageSize == null)
                return;

            _joinMessageSize.Value = Math.Clamp(value, 50, 100);
            MelonPreferences.Save();
        }

        public static void SetLeaveMessageSize(int value)
        {
            if (_leaveMessageSize == null)
                return;

            _leaveMessageSize.Value = Math.Clamp(value, 50, 100);
            MelonPreferences.Save();
        }

        private IEnumerator LoadReferences()
        {
            while ((Object)(object)_playerReferenceManager == (Object)null)
            {
                _playerReferenceManager = PlayerReferenceManager.Instance;
                yield return null;
            }
            // List<StatueSetup> statueSetups = null;
            // while (statueSetups == null || statueSetups.Count == 0)
            // {
            //     statueSetups = ((IEnumerable<StatueSetup>)Object.FindObjectsByType<StatueSetup>((FindObjectsSortMode)0)).ToList();
            //     Debug.Log("[Core] Waiting for scene references to load...");
            //     Debug.Log("Count: " + statueSetups.Count);
            //     yield return null;
            // }

            // Object.FindObjectsByType<StatueSetup>((FindObjectsSortMode)0).ToList();
            ReferencesLoaded = true;
            Debug.Log("[Core] Scene references loaded successfully.");
        }
    }
}

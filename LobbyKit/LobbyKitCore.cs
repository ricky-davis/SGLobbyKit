using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using Il2CppFishNet;
using MelonLoader;
using MelonLoader.NativeUtils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LobbyKit
{
    public class LobbyKitCore : MelonMod
    {
        public static LobbyKitCore Instance;
        private PlayerReference localPlayer;
        private readonly List<PlayerReference> players = new List<PlayerReference>();
        private readonly Dictionary<int, double> _playerJoinTimes = new Dictionary<int, double>();
        private readonly Dictionary<string, double> _playerJoinTimesByProductId = new Dictionary<string, double>();
        public static bool isHost = false;
        public static bool WasHosting = false;

        private static MelonPreferences_Category _preferences;
        private static MelonPreferences_Category _serverSettings;   // shared with SledHeadless
        private static MelonPreferences_Entry<bool> _enableGuestBangCommands;
        private static MelonPreferences_Entry<string> _serverName;
        private static MelonPreferences_Entry<int> _serverCapacity;
#pragma warning disable CS0649
        private static MelonPreferences_Entry<int> _searchMinPlayers;
#pragma warning restore CS0649
        private static MelonPreferences_Entry<int> _searchMaxPlayers;
        private static MelonPreferences_Entry<bool> _searchShowLocked;
        private static MelonPreferences_Entry<bool> _searchShowModded;
        private static MelonPreferences_Entry<bool> _searchShowYourLanguageOnly;
        private static MelonPreferences_Entry<bool> _searchOnlyPeacefulLobbies;
        private static MelonPreferences_Entry<bool> _searchHidePeacefulLobbies;
        private static MelonPreferences_Entry<int> _searchRegionMode;
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
        private static MelonPreferences_Entry<bool> _autoRestartOnCrash;
        private static MelonPreferences_Entry<bool> _blockThrowingSpam;
        private static MelonPreferences_Entry<bool> _blockPlayerSizeCheat;
        private static MelonPreferences_Entry<bool> _blockFlyingSleds;
        private static MelonPreferences_Entry<System.Collections.Generic.List<string>> _chatPrefixes;

        public static bool EnableGuestBangCommands => _enableGuestBangCommands?.Value ?? true;
        public static string ServerName => _serverName?.Value ?? string.Empty;
        public static int ServerCapacity => _serverCapacity?.Value ?? 8;
        public static int SearchMinPlayers => _searchMinPlayers?.Value ?? 1;
        public static int SearchMaxPlayers => _searchMaxPlayers?.Value ?? 0; // 0 == Any
        public static bool SearchShowLocked => _searchShowLocked?.Value ?? true;
        public static bool SearchShowModded => _searchShowModded?.Value ?? true;
        public static bool SearchShowYourLanguageOnly => _searchShowYourLanguageOnly?.Value ?? false;
        public static bool SearchOnlyPeacefulLobbies => _searchOnlyPeacefulLobbies?.Value ?? false;
        public static bool SearchHidePeacefulLobbies => _searchHidePeacefulLobbies?.Value ?? false;
        public static int SearchRegionMode => _searchRegionMode?.Value ?? 1;
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
        public static bool AutoRestartOnCrash => _autoRestartOnCrash?.Value ?? false;
        public static bool BlockThrowingSpam => _blockThrowingSpam?.Value ?? true;
        public static bool BlockPlayerSizeCheat => _blockPlayerSizeCheat?.Value ?? true;
        public static bool BlockFlyingSleds => _blockFlyingSleds?.Value ?? true;

        // Chat prefixes by permission level, stored as one list [Mod, Admin, Owner] (index = (int)level - 1).
        // Empty string disables that level's prefix.
        public static string ChatPrefixFor(Features.Permissions.PermLevel level)
        {
            int idx = (int)level - 1; // Mod(1)->0, Admin(2)->1, Owner(3)->2
            var list = _chatPrefixes?.Value;
            return (list != null && idx >= 0 && idx < list.Count && list[idx] != null) ? list[idx] : string.Empty;
        }

        private PlayerReferenceManager _playerReferenceManager;

        public bool ReferencesLoaded = false;
        public void SavePrefs()
        {
            MelonPreferences.Save();
        }

        public override unsafe void OnInitializeMelon()
        {
            Instance = this;

            _preferences = MelonPreferences.CreateCategory("LobbyKit", "LobbyKit");
            _enableGuestBangCommands = _preferences.CreateEntry("EnableGuestBangCommands", true, "Enable Guest Bang Commands", "Allow non-host players to use custom bang chat commands like !tp.");

            // Server config lives in its own shared category so LobbyKit and SledHeadless read one source of truth.
            // GetOrCreate (not CreateEntry): LobbyKit and SledHeadless share this category, and CreateEntry
            // throws if the entry already exists, so the second mod to load must reuse the existing entries.
            _serverSettings = MelonPreferences.CreateCategory("ServerSettings", "Server Settings");
            _serverName = GetOrCreate(_serverSettings, "ServerName", string.Empty, "Server Name", "Custom default lobby/server name. Leave empty to use '<PlayerName>\'s Lobby'.");
            _serverCapacity = GetOrCreate(_serverSettings, "ServerCapacity", 8, "Server Capacity", "Saved default value for the max players slider.");
            _isPublicLobby = GetOrCreate(_serverSettings, "IsPublicLobby", true, "Public Lobby", "Saved default for public/private lobby.");
            _isPasswordProtected = GetOrCreate(_serverSettings, "IsPasswordProtected", false, "Password Protected", "Saved default for password protection.");
            _lobbyPassword = GetOrCreate(_serverSettings, "LobbyPassword", string.Empty, "Lobby Password", "Saved default lobby password.");
            _isPeacefulMode = GetOrCreate(_serverSettings, "IsPeacefulMode", false, "Peaceful Mode", "Saved default for peaceful mode.");
            _isTextChatOnly = GetOrCreate(_serverSettings, "IsTextChatOnly", false, "Text Chat Only", "Saved default for text-chat-only mode.");

            // One-time migration: pull any non-default value users saved under the old "LobbyKit" category into
            // ServerSettings, then drop the stale key. No-ops once migrated (ServerSettings already non-default).
            MigrateServerSetting(_preferences, "ServerName", _serverName, string.Empty);
            MigrateServerSetting(_preferences, "ServerCapacity", _serverCapacity, 8);
            MigrateServerSetting(_preferences, "IsPublicLobby", _isPublicLobby, true);
            MigrateServerSetting(_preferences, "IsPasswordProtected", _isPasswordProtected, false);
            MigrateServerSetting(_preferences, "LobbyPassword", _lobbyPassword, string.Empty);
            MigrateServerSetting(_preferences, "IsPeacefulMode", _isPeacefulMode, false);
            MigrateServerSetting(_preferences, "IsTextChatOnly", _isTextChatOnly, false);

            // Disabled for now. Leave the field/property/setter in place so this can be re-enabled without rewiring callers.
            // _searchMinPlayers = _preferences.CreateEntry("SearchMinPlayers", 1, "Search Min Players", "Minimum players filter for lobby search.");
            _searchMaxPlayers = _preferences.CreateEntry("SearchMaxPlayers", 0, "Search Max Players", "Maximum players filter for lobby search. 0 == Any.");
            _searchShowLocked = _preferences.CreateEntry("SearchShowLocked", true, "Search Show Locked", "Include locked lobbies in search results.");
            _searchShowModded = _preferences.CreateEntry("SearchShowModded", true, "Search Show Modded", "Include modded lobbies in search results.");
            _searchShowYourLanguageOnly = _preferences.CreateEntry("SearchShowYourLanguageOnly", false, "Search Your Language Only", "Filter to language-matching lobbies.");
            _searchOnlyPeacefulLobbies = _preferences.CreateEntry("SearchOnlyPeacefulLobbies", false, "Search Only Peaceful Lobbies", "Only show peaceful-mode lobbies in search results.");
            _searchHidePeacefulLobbies = _preferences.CreateEntry("SearchHidePeacefulLobbies", false, "Search Hide Peaceful Lobbies", "Hide peaceful-mode lobbies in search results.");
            _searchRegionMode = _preferences.CreateEntry("SearchRegionMode", 1, "Search Region Mode", "Lobby search region filter: 0 near, 1 nearby regions, 2 far, 3 worldwide.");
            _messageOfTheDay = _preferences.CreateEntry("MessageOfTheDay", string.Empty, "Message of the Day", "Private chat message sent to each player when they join your hosted lobby. Leave empty to disable.");
            _showJoinMessages = _preferences.CreateEntry("ShowJoinMessages", true, "Show Join Messages", "Broadcast a chat message when a player joins your hosted lobby.");
            _showLeaveMessages = _preferences.CreateEntry("ShowLeaveMessages", true, "Show Leave Messages", "Broadcast a chat message when a player leaves your hosted lobby.");
            _joinMessageSize = _preferences.CreateEntry("JoinMessageSize", 75, "Join Message Size", "Font size percentage for join messages (e.g. 75 for 75%).");
            _leaveMessageSize = _preferences.CreateEntry("LeaveMessageSize", 75, "Leave Message Size", "Font size percentage for leave messages (e.g. 75 for 75%).");
            _autoRestartOnCrash = _preferences.CreateEntry("AutoRestartOnCrash", false, "Auto-Restart On Crash", "Automatically re-host the lobby when it crashes unexpectedly.");
            _blockThrowingSpam = _preferences.CreateEntry("BlockThrowingSpam", false, "Block Throwing Spam", "Anticheat: rate-limit and kick clients who spam server RPCs (e.g. rapid throwing).");
            _blockPlayerSizeCheat = _preferences.CreateEntry("BlockPlayerSizeCheat", true, "Block Player Size Cheat", "Anticheat: force each player's avatar to their allowed size (default 1, or their !size choice) and clamp any cheated scale back.");
            _blockFlyingSleds = _preferences.CreateEntry("BlockFlyingSleds", true, "Block Flying Sleds", "Anticheat: no-op the client-initiated Cmd_PushSled (a raw AddForce lever used to fly sleds). Boosts use a separate path and are unaffected.");
            _chatPrefixes = _preferences.CreateEntry("ChatPrefixes",
                new System.Collections.Generic.List<string> { "<#7DFF7D>[Mod]</color> ", "<#7DD0FF>[Admin]</color> ", "<#FFE066>[Owner]</color> " },
                "Chat Prefixes",
                "Prefixes shown before a name in chat, by level: [Mod, Admin, Owner]. Empty string disables that level's prefix.");

            Features.Permissions.Perms.Initialize();
            Patches.ChatSystem.SeedCommandLevels();
            MelonPreferences.Save();

            HarmonyInstance.PatchAll();
            Features.Anticheat.GenericServerRpcRateLimitPatch.ApplyPatches(HarmonyInstance);
            Features.Anticheat.PlayerScalePacketClamp.Apply(HarmonyInstance);
            Features.Anticheat.SledPushBlockPatch.Apply(HarmonyInstance);
            Features.Fixes.CannonLaunchFix.Apply(HarmonyInstance);

            MelonCoroutines.Start(PlayerCountLogLoop());
        }

        // GetEntry if it already exists (e.g. SledHeadless created it on the shared ServerSettings category),
        // otherwise CreateEntry. MelonLoader's CreateEntry throws on a duplicate identifier.
        private static MelonPreferences_Entry<T> GetOrCreate<T>(MelonPreferences_Category category, string identifier, T defaultValue, string displayName, string description = null)
        {
            return category.HasEntry(identifier)
                ? category.GetEntry<T>(identifier)
                : category.CreateEntry(identifier, defaultValue, displayName, description);
        }

        // Copies a value users previously saved under an old category into its new ServerSettings entry, then
        // removes the stale key. Only overwrites when the ServerSettings entry is still at its default, so an
        // already-migrated or explicitly-set value is never clobbered (and it no-ops on subsequent launches).
        private static void MigrateServerSetting<T>(MelonPreferences_Category oldCategory, string key, MelonPreferences_Entry<T> target, T defaultValue)
        {
            try
            {
                if (oldCategory == null || target == null)
                    return;

                MelonPreferences_Entry<T> old = oldCategory.CreateEntry<T>(key, defaultValue);   // binds to the saved value if one exists
                bool oldIsDefault = EqualityComparer<T>.Default.Equals(old.Value, defaultValue);
                bool targetIsDefault = EqualityComparer<T>.Default.Equals(target.Value, defaultValue);
                if (targetIsDefault && !oldIsDefault)
                    target.Value = old.Value;

                oldCategory.DeleteEntry(key);   // drop the stale key from the old category
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[LobbyKit] ServerSettings migrate '{key}' failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // Runs after all gameplay Updates each frame; re-asserts player scale so a size cheat can't stick.
        public override void OnLateUpdate()
        {
            if (BlockPlayerSizeCheat)
                Features.Anticheat.PlayerScaleEnforcer.Tick();
        }

        // Number of connected non-host players (excludes the host's own clientHost connection 32767).
        private int RemotePlayerCount => players.Count(pl => pl != null && pl.ConnectionID != 32767);

        // Periodically logs the connected player count (and names) while hosting, as a server heartbeat.
        private IEnumerator PlayerCountLogLoop()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(60f);
                if (!isHost)
                    continue;
                int n = RemotePlayerCount;
                string list = n > 0
                    ? " [" + string.Join(", ", players
                        .Where(pl => pl != null && pl.ConnectionID != 32767)
                        .Select(pl => Patches.ChatSystem.StripRichText(string.IsNullOrWhiteSpace(pl.Username) ? "?" : pl.Username))) + "]"
                    : "";
                MelonLogger.Msg($"[LobbyKit] Players online: {n}{list}");
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            ReferencesLoaded = false;
            ResetLobbyTrackingState("scene loaded");
            Patches.ChatSystem.ResetSessionState();
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

        // ───────────────────────── Dedicated / headless server support ─────────────────────────
        // PlayerJoinPatch / PlayerLeavePatch above hook PlayerReferenceManager.OnPlayerReferenceAdded /
        // OnPlayerReferenceRemoved. Those game methods only run when the SyncList
        // sync_PlayerReferences.OnChange callback is subscribed — which the game does ONLY in
        // PlayerReferenceManager.OnStartClient. An avatar-less headless host never wires that up, so on it
        // those hooks never fire and MOTD / join / leave go silent. We drive the SAME PlayerJoinedGame /
        // PlayerLeftGame off the server-authoritative Server_AddPlayerReference / Server_RemovePlayerReference
        // instead.
        //
        // GATE = Application.isBatchMode. IMPORTANT: a headless host is NOT FishNet "server-only" — it is
        // booted through the normal HOST path, so it is a clientHost: IsServerStarted AND IsClientStarted
        // are BOTH true (verified live), hence IsServerOnlyStarted == false. It just has no local avatar.
        // So IsServerOnlyStarted is the wrong discriminator here; the reliable "this is a dedicated/headless
        // server" signal is Application.isBatchMode (the server is always launched -batchmode -nographics,
        // and SledHeadless gates its whole headless mode on it). A normal graphical client-host has
        // isBatchMode == false and keeps using the OnPlayerReferenceAdded path, so the two paths never
        // collide — and LobbyKit's own isNewConnection / MotdRecipients / wasTrackedPlayer dedup makes any
        // overlap idempotent regardless. We call PlayerJoinedGame / PlayerLeftGame directly rather than
        // re-running the game's OnPlayerReferenceAdded, whose tail (WarmCommunicationPolicy, EOS host-only
        // state) NREs on an avatar-less host.
        [HarmonyPatch(typeof(PlayerReferenceManager), "Server_AddPlayerReference")]
        public static class DedicatedServerJoinPatch
        {
            private static void Postfix(PlayerReferenceManager __instance, int connectionId)
            {
                if (Instance == null || !Application.isBatchMode)
                    return;
                if (connectionId == 32767)
                    return; // the host's own dict-only registration, not a joining player

                isHost = true; // a dedicated server IS the host; no local PlayerReference will set this
                PlayerReference joined = FindByConnectionId(__instance, connectionId);
                if (joined != null)
                    Instance.PlayerJoinedGame(joined);
            }
        }

        [HarmonyPatch(typeof(PlayerReferenceManager), "Server_RemovePlayerReference")]
        public static class DedicatedServerLeavePatch
        {
            // Prefix: the original removes the reference from sync_PlayerReferences in its own body, so we
            // must resolve it BEFORE the original runs.
            private static void Prefix(PlayerReferenceManager __instance, int playerNetId)
            {
                if (Instance == null || !Application.isBatchMode)
                    return;
                if (playerNetId == 32767)
                    return;

                isHost = true;
                PlayerReference leaving = FindByConnectionId(__instance, playerNetId);
                if (leaving != null)
                    Instance.PlayerLeftGame(leaving);
            }
        }

        private static PlayerReference FindByConnectionId(PlayerReferenceManager prm, int connectionId)
        {
            var refs = prm.GetPlayerReferences();
            if (refs == null)
                return null;
            for (int i = 0; i < refs.Count; i++)
            {
                var r = refs[i];
                if (r != null && r.ConnectionID == connectionId)
                    return r;
            }
            return null;
        }

        public void PlayerJoinedGame(PlayerReference p)
        {
            if (p == null)
                return;

            // Ban enforcement: kick a banned PUID the moment they register (host-side only).
            if (isHost && p.ConnectionID != 32767 && !p.IsLocalPlayerInstance() &&
                Features.Permissions.Perms.IsBanned(p.ProductUserId))
            {
                Features.Anticheat.KickAnnouncer.Enqueue(p.ConnectionID, "is banned");
                try { InstanceFinder.ServerManager?.Kick(p.ConnectionID, Il2CppFishNet.Managing.Server.KickReason.Unset); } catch { }
                MelonLogger.Msg($"[LobbyKit] Banned player kicked on join (conn {p.ConnectionID}, puid {p.ProductUserId}).");
                return;
            }

            // Keep stored op/ban names current for anyone tracked in the op-file.
            if (isHost && p.ConnectionID != 32767)
                Features.Permissions.Perms.RememberName(p.ProductUserId, Patches.ChatSystem.StripRichText(p.Username));

            double nowUptime = GetLobbyUptimeSeconds();
            bool isLocalPlayer = p.IsLocalPlayerInstance();
            bool isNewConnection = !players.Any(player => player != null && player.ConnectionID == p.ConnectionID);
            if (isNewConnection)
                players.Add(p);

            if (isHost || isLocalPlayer)
            {
                // Guard against stale timestamps carried across lobby transitions where
                // remove events were missed. Old values often exceed current uptime.
                if (!string.IsNullOrWhiteSpace(p.ProductUserId) &&
                    _playerJoinTimesByProductId.TryGetValue(p.ProductUserId, out double trackedJoinTime) &&
                    trackedJoinTime > nowUptime + 1d)
                {
                    _playerJoinTimesByProductId[p.ProductUserId] = nowUptime;
                }

                if (isNewConnection)
                    TrackPlayerJoinTime(p, nowUptime);
            }

            if (isLocalPlayer)
            {
                localPlayer = p;
                isHost = p.ConnectionID == 32767;
                if (isHost)
                {
                    WasHosting = true;
                    Patches.SilentCrashDetectionPatches.StartPolling();
                }
            }
            else if (isHost && isNewConnection)
            {
                if (ShowJoinMessages)
                {
                    string username = Patches.ChatSystem.AutoCloseTmpRichText(string.IsNullOrWhiteSpace(p.Username) ? "A player" : p.Username);
                    Patches.ChatSystem.BroadcastSystemMessage($"<size={JoinMessageSize}%><#FA0>{username} joined.");
                }
                string logName = Patches.ChatSystem.StripRichText(string.IsNullOrWhiteSpace(p.Username) ? "A player" : p.Username);
                MelonLogger.Msg($"[LobbyKit] + {logName} joined (conn {p.ConnectionID}, puid {p.ProductUserId}) — {RemotePlayerCount} online.");

                // A late joiner spawns existing players from the server's cached transform and missed any earlier
                // !size ObserversUpdate, so re-broadcast registered sizes across their load window.
                if (Features.Anticheat.PlayerSizeRegistry.AnySizes)
                    MelonCoroutines.Start(RepushSizesForJoin());
            }

            if (isHost)
                Patches.ChatSystem.SendMotdToPlayer(p);
                
            try
            {
                string cID = InstanceFinder.TransportManager.Transport.GetConnectionAddress(p.ConnectionID);
                Debug.Log($"Client {p.ConnectionID} connection ID: {cID}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error occurred while fetching connection ID for client {p.ConnectionID}: {ex.Message}");
            }
        }

        public void PlayerLeftGame(PlayerReference removedPlayer)
        {
            if (removedPlayer == null)
                return;

            bool wasTrackedPlayer = players.Any(player => player != null && player.ConnectionID == removedPlayer.ConnectionID);
            bool isLocalPlayer = removedPlayer.IsLocalPlayerInstance();
            if (isHost && wasTrackedPlayer && !isLocalPlayer)
            {
                string username = Patches.ChatSystem.AutoCloseTmpRichText(string.IsNullOrWhiteSpace(removedPlayer.Username) ? "A player" : removedPlayer.Username);
                string logName = Patches.ChatSystem.StripRichText(string.IsNullOrWhiteSpace(removedPlayer.Username) ? "A player" : removedPlayer.Username);
                int remaining = RemotePlayerCount - 1; // leaver still counted until RemoveAll below
                if (Features.Anticheat.KickAnnouncer.TryConsume(removedPlayer.ConnectionID, out string kickReason))
                {
                    Patches.ChatSystem.BroadcastSystemMessage($"<size={LeaveMessageSize}%><#F44>{username} {kickReason}.");
                    MelonLogger.Msg($"[LobbyKit] - {logName} {kickReason} (conn {removedPlayer.ConnectionID}) — {remaining} online.");
                }
                else
                {
                    if (ShowLeaveMessages)
                        Patches.ChatSystem.BroadcastSystemMessage($"<size={LeaveMessageSize}%><#FA0>{username} left.");
                    MelonLogger.Msg($"[LobbyKit] - {logName} left (conn {removedPlayer.ConnectionID}) — {remaining} online.");
                }
            }

            players.RemoveAll(player => player == null || player.ConnectionID == removedPlayer.ConnectionID);
            _playerJoinTimes.Remove(removedPlayer.ConnectionID);
            if (!string.IsNullOrWhiteSpace(removedPlayer.ProductUserId))
                _playerJoinTimesByProductId.Remove(removedPlayer.ProductUserId);
            Patches.ChatSystem.ForgetMotdRecipient(removedPlayer.ConnectionID);
            Patches.ChatSystem.ForgetTeleportRequests(removedPlayer.ConnectionID);
            Features.Anticheat.PlayerSizeRegistry.Remove(removedPlayer.ConnectionID);

            if (localPlayer != null && localPlayer.ConnectionID == removedPlayer.ConnectionID)
            {
                localPlayer = null;
                isHost = false;
                Patches.SilentCrashDetectionPatches.StopPolling();
                ResetLobbyTrackingState("local player removed");
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

        public bool TryGetPlayerJoinTime(PlayerReference playerReference, out double joinTimeSeconds)
        {
            joinTimeSeconds = 0d;

            if (!isHost || playerReference == null)
                return false;

            return _playerJoinTimes.TryGetValue(playerReference.ConnectionID, out joinTimeSeconds);
        }

        public bool TryGetPlayerSessionDuration(PlayerReference playerReference, out double sessionSeconds)
        {
            sessionSeconds = 0d;

            if (!isHost || playerReference == null)
                return false;

            double currentUptimeSeconds = GetLobbyUptimeSeconds();
            if (!_playerJoinTimes.TryGetValue(playerReference.ConnectionID, out double joinTimeSeconds))
            {
                joinTimeSeconds = playerReference.ConnectionID == 32767 ? 0d : currentUptimeSeconds;
                TrackPlayerJoinTime(playerReference, joinTimeSeconds);
            }
            else if (joinTimeSeconds > currentUptimeSeconds + 1d)
            {
                joinTimeSeconds = playerReference.ConnectionID == 32767 ? 0d : currentUptimeSeconds;
                TrackPlayerJoinTime(playerReference, joinTimeSeconds);
            }

            sessionSeconds = currentUptimeSeconds - joinTimeSeconds;
            return true;
        }

        public bool TryGetPlayerSessionDuration(string productUserId, out double sessionSeconds)
        {
            sessionSeconds = 0d;

            if (!isHost || string.IsNullOrWhiteSpace(productUserId))
                return false;

            double currentUptimeSeconds = GetLobbyUptimeSeconds();
            if (!_playerJoinTimesByProductId.TryGetValue(productUserId, out double joinTimeSeconds))
            {
                joinTimeSeconds = currentUptimeSeconds;
                _playerJoinTimesByProductId[productUserId] = joinTimeSeconds;
            }
            else if (joinTimeSeconds > currentUptimeSeconds + 1d)
            {
                joinTimeSeconds = currentUptimeSeconds;
                _playerJoinTimesByProductId[productUserId] = joinTimeSeconds;
            }

            sessionSeconds = currentUptimeSeconds - joinTimeSeconds;
            return true;
        }

        public bool TryGetPlayerListTimerDuration(string productUserId, bool isLobbyOwner, out double sessionSeconds)
        {
            sessionSeconds = 0d;

            if (string.IsNullOrWhiteSpace(productUserId))
                return false;

            if (!isHost)
            {
                double currentUptimeSeconds = GetLobbyUptimeSeconds();
                if (isLobbyOwner)
                {
                    sessionSeconds = currentUptimeSeconds;
                    return true;
                }

                if (!IsLocalPlayerProductId(productUserId))
                    return false;

                if (!_playerJoinTimesByProductId.TryGetValue(productUserId, out double joinTimeSeconds))
                {
                    joinTimeSeconds = currentUptimeSeconds;
                    _playerJoinTimesByProductId[productUserId] = joinTimeSeconds;
                }

                sessionSeconds = currentUptimeSeconds - joinTimeSeconds;
                return true;
            }

            return TryGetPlayerSessionDuration(productUserId, out sessionSeconds);
        }

        private bool IsLocalPlayerProductId(string productUserId)
        {
            return localPlayer != null
                && !string.IsNullOrWhiteSpace(localPlayer.ProductUserId)
                && string.Equals(localPlayer.ProductUserId, productUserId, StringComparison.Ordinal);
        }

        private void TrackPlayerJoinTime(PlayerReference playerReference, double joinTimeSeconds)
        {
            _playerJoinTimes[playerReference.ConnectionID] = joinTimeSeconds;

            if (!string.IsNullOrWhiteSpace(playerReference.ProductUserId))
                _playerJoinTimesByProductId[playerReference.ProductUserId] = joinTimeSeconds;
        }

        public void ResetLobbyTrackingState(string reason = null)
        {
            players.Clear();
            _playerJoinTimes.Clear();
            _playerJoinTimesByProductId.Clear();
            if (!string.IsNullOrWhiteSpace(reason))
                Debug.Log($"[LobbyKit] Reset lobby tracking state ({reason}).");
        }

        public static string FormatLobbyJoinTime(double uptimeSeconds)
        {
            if (uptimeSeconds < 0d)
                uptimeSeconds = 0d;

            TimeSpan timeSpan = TimeSpan.FromSeconds(uptimeSeconds);
            if (timeSpan.TotalHours >= 1d)
                return $"{(int)timeSpan.TotalHours}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}";

            return $"{(int)timeSpan.TotalMinutes}:{timeSpan.Seconds:00}";
        }

        private static double GetLobbyUptimeSeconds()
        {
            var timeManager = InstanceFinder.TimeManager;
            if (timeManager == null)
                return Time.unscaledTimeAsDouble;

            if (timeManager.Tick > 0)
                return timeManager.TicksToTime(timeManager.Tick);

            if (timeManager.ServerUptime > 0f)
                return timeManager.ServerUptime;

            if (timeManager.ClientUptime > 0f)
                return timeManager.ClientUptime;

            return Time.unscaledTimeAsDouble;
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

        public static void SetSearchMinPlayers(int value)
        {
            if (_searchMinPlayers == null)
                return;

            _searchMinPlayers.Value = Math.Clamp(value, 1, 64);
            MelonPreferences.Save();
        }

        public static void SetSearchMaxPlayers(int value)
        {
            if (_searchMaxPlayers == null)
                return;

            _searchMaxPlayers.Value = value == 0 ? 0 : Math.Clamp(value, 1, 64);
            MelonPreferences.Save();
        }

        public static void SetSearchShowLocked(bool value)
        {
            if (_searchShowLocked == null)
                return;

            _searchShowLocked.Value = value;
            MelonPreferences.Save();
        }

        public static void SetSearchShowModded(bool value)
        {
            if (_searchShowModded == null)
                return;

            _searchShowModded.Value = value;
            MelonPreferences.Save();
        }

        public static void SetSearchShowYourLanguageOnly(bool value)
        {
            if (_searchShowYourLanguageOnly == null)
                return;

            _searchShowYourLanguageOnly.Value = value;
            MelonPreferences.Save();
        }

        public static void SetSearchOnlyPeacefulLobbies(bool value)
        {
            if (_searchOnlyPeacefulLobbies == null)
                return;

            _searchOnlyPeacefulLobbies.Value = value;
            if (value && _searchHidePeacefulLobbies != null)
                _searchHidePeacefulLobbies.Value = false;

            MelonPreferences.Save();
        }

        public static void SetSearchHidePeacefulLobbies(bool value)
        {
            if (_searchHidePeacefulLobbies == null)
                return;

            _searchHidePeacefulLobbies.Value = value;
            if (value && _searchOnlyPeacefulLobbies != null)
                _searchOnlyPeacefulLobbies.Value = false;

            MelonPreferences.Save();
        }

        public static void SetSearchRegionMode(int value)
        {
            if (_searchRegionMode == null)
                return;

            _searchRegionMode.Value = Math.Clamp(value, 0, 3);
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

        public static void SetBlockThrowingSpam(bool value)
        {
            if (_blockThrowingSpam == null) return;
            _blockThrowingSpam.Value = value;
            MelonPreferences.Save();
        }

        public static void SetBlockPlayerSizeCheat(bool value)
        {
            if (_blockPlayerSizeCheat == null) return;
            _blockPlayerSizeCheat.Value = value;
            MelonPreferences.Save();
        }

        public static void SetBlockFlyingSleds(bool value)
        {
            if (_blockFlyingSleds == null) return;
            _blockFlyingSleds.Value = value;
            MelonPreferences.Save();
        }

        // Sets a single level's chat prefix within the ChatPrefixes list (index = (int)level - 1), growing the
        // list as needed so any of Mod/Admin/Owner can be set independently.
        public static void SetChatPrefix(Features.Permissions.PermLevel level, string value)
        {
            if (_chatPrefixes == null) return;
            int idx = (int)level - 1; // Mod(1)->0, Admin(2)->1, Owner(3)->2
            if (idx < 0) return;

            var list = _chatPrefixes.Value ?? new System.Collections.Generic.List<string>();
            while (list.Count <= idx) list.Add(string.Empty);
            list[idx] = value ?? string.Empty;
            _chatPrefixes.Value = list;
            MelonPreferences.Save();
        }

        public static void SetAutoRestartOnCrash(bool value)
        {
            if (_autoRestartOnCrash == null)
                return;

            _autoRestartOnCrash.Value = value;
            MelonPreferences.Save();
        }

        // Re-broadcasts registered !size values a few times across a joiner's load window so they pick up sizes
        // applied before they arrived (an ObserversUpdate only reaches clients already observing the player).
        private static IEnumerator RepushSizesForJoin()
        {
            float[] increments = { 0.0f, 0.1f, 2f, 2f, 3f, 5f };   // re-push at ~2, 4, 7, 12s after join
            foreach (float inc in increments)
            {
                yield return new WaitForSecondsRealtime(inc);
                Features.Anticheat.PlayerScalePacketClamp.RepushAllSizes();
            }
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

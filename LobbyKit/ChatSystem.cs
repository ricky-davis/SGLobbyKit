using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppFishNet;
using Il2CppFishNet.Connection;
using Il2CppFishNet.Managing.Server;
using Il2CppDissonance.Integrations.FishNet;
using Il2Cpp_Scripts.Player;
using Il2Cpp_Scripts.Systems.Chat;
using MelonLoader;
using LobbyKit.Features.Settings;
using LobbyKit.Features.Permissions;
using UnityEngine;
using System.Text.RegularExpressions;

namespace LobbyKit.Patches
{
    // Command handlers live in partial-class files under Features/Commands/ (ChatSystem.*.cs); the framework
    // (chat patches, dispatch, permissions, broadcast, confirmation, help) stays here.
    [HarmonyPatch]
    public static partial class ChatSystem
    {
        private const int HostConnectionId = 32767;
        private const int PrivateReplySystemMessageType = 0;
        private const string MotdCommand = "!motd";
        private const string HideSystemMessageSuffixTag = "<size=0>";

        private delegate void ChatCommandHandler(PlayerControl playerControl, string args);

        private sealed class CommandDefinition
        {
            public CommandDefinition(
                ChatCommandHandler handler,
                string usage,
                string description,
                PermLevel minLevel = PermLevel.Everyone,
                bool hiddenFromHelp = false)
            {
                Handler = handler;
                Usage = usage;
                Description = description;
                MinLevel = minLevel;
                HiddenFromHelp = hiddenFromHelp;
            }

            public ChatCommandHandler Handler { get; }
            public string Usage { get; }
            public string Description { get; }
            public PermLevel MinLevel { get; }   // minimum level to invoke the command at all
            public bool HiddenFromHelp { get; }
        }

        private static readonly Dictionary<int, (string Message, int Frame)> LastCommandBySource = new();
        private static readonly Dictionary<int, string> LastExplicitCommandBySource = new();
        private static readonly HashSet<int> MotdRecipients = new();
        private static readonly Dictionary<int, int> TeleportRequests = new();

        private static readonly Dictionary<string, CommandDefinition> Commands = new(StringComparer.OrdinalIgnoreCase)
        {
            ["!help"] = new CommandDefinition(
                HandleHelpCommand,
                "!help [command]",
                "Shows available commands or details for one command.",
                hiddenFromHelp: true),
            ["!settings"] = new CommandDefinition(
                OpenSettingsMenu,
                "!settings",
                "Open LobbyKit settings.",
                minLevel: PermLevel.Admin),
            [MotdCommand] = new CommandDefinition(
                HandleMotdCommand,
                "!motd [message]",
                "Show the message of the day. Admins can pass text to set it."),
            ["!bc"] = new CommandDefinition(
                HandleBangCommandsCommand,
                "!bc [on|off]",
                "Enable or disable guest bang commands.",
                minLevel: PermLevel.Admin),
            ["!tp"] = new CommandDefinition(
                HandleTpCommand,
                "!tp [name]",
                "Teleport to a player by name."),
            ["!tpme"] = new CommandDefinition(
                HandleTpMeCommand,
                "!tpme [name]",
                "Ask a player to Teleport to you."),
            ["!tpa"] = new CommandDefinition(
                HandleTpAcceptCommand,
                "!tpa",
                "Accept a Teleport request.",
                hiddenFromHelp: true),
            ["!tpf"] = new CommandDefinition(
                HandleTpForceCommand,
                "!tpf [name]",
                "Force a player to Teleport to you.",
                minLevel: PermLevel.Mod),
            ["!size"] = new CommandDefinition(
                HandleSizeCommand,
                "!size [0.2-3.0]",
                "Set your player size. !size 1 resets to normal."),
            ["!kick"] = new CommandDefinition(
                HandleKickCommand,
                "!kick [name] [reason]",
                "Kick a player.",
                minLevel: PermLevel.Mod),
            ["!ban"] = new CommandDefinition(
                HandleBanCommand,
                "!ban [name|puid] [reason]",
                "Ban a player (use !unban to reverse).",
                minLevel: PermLevel.Admin),
            ["!unban"] = new CommandDefinition(
                HandleUnbanCommand,
                "!unban [name|puid]",
                "Remove a ban by player name or PUID.",
                minLevel: PermLevel.Admin),
            ["!op"] = new CommandDefinition(
                HandleOpCommand,
                "!op [level] [name]",
                "Set a player's level (everyone/mod/admin/owner). Requires confirmation.",
                minLevel: PermLevel.Owner),
            ["!level"] = new CommandDefinition(
                HandleLevelCommand,
                "!level [name]",
                "Show your level, or another player's.",
                minLevel: PermLevel.Mod),
            ["!confirm"] = new CommandDefinition(
                HandleConfirmCommand,
                "!confirm",
                "Confirm a pending action (kick, ban, op, etc.).",
                hiddenFromHelp: true)
        };

        // Seeds the op-file with each command's default required level (without overwriting existing entries),
        // so the JSON lists every command and the operator can edit its level. Set a level above Owner to
        // disable a command. Called once at startup after Perms is loaded.
        internal static void SeedCommandLevels()
        {
            var defaults = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in Commands)
                defaults[entry.Key] = (int)entry.Value.MinLevel;
            Perms.SeedCommandLevels(defaults);
        }

        private static void OpenSettingsMenu(PlayerControl playerControl, string args)
        {
            SettingsMenuController.Instance.Open(SettingsMenuView.Build, SettingsMenuView.HasRequiredTemplates);
        }

        [HarmonyPatch(typeof(ChatManager), "ProcessChatInput")]
        [HarmonyPrefix]
        private static bool ChatManager_ProcessChatInput_Prefix(ChatManager __instance)
        {
            var localPlayer = LobbyKitCore.Instance?.GetLocalPlayer();
            if (!LobbyKitCore.isHost || localPlayer == null || localPlayer.ConnectionID != HostConnectionId)
                return true;

            var chatBox = __instance?.chatBox;
            string raw = chatBox?.InputField?.text ?? string.Empty;
            if (raw.StartsWith("/", StringComparison.Ordinal))
                return true;

            string message = StringHelpers.PrepareStringForChat(raw);
            if (string.IsNullOrWhiteSpace(message))
                return true;

            if (WasHandledThisFrame(localPlayer.ConnectionID, message))
                return false;

            bool handledCommand = TryHandleCommand(message, localPlayer.ConnectionID, isHostLocal: true);
            if (!handledCommand)
                BroadcastMessage(0, message, AutoCloseTmpRichText(ApplyLevelPrefix(localPlayer.ConnectionID, localPlayer.Username)), showAboveUser: localPlayer.ConnectionID);

            chatBox.inputFieldValue = string.Empty;
            chatBox.ClearInputBox();
            MarkHandled(localPlayer.ConnectionID, message);
            return false;
        }

        [HarmonyPatch(typeof(ChatManager), "OnServerReceivedChatBroadcastFromClient")]
        [HarmonyPrefix]
        private static bool ChatManager_OnServerReceivedChatBroadcastFromClient_Prefix(
            NetworkConnection networkConnection,
            ChatMessage chatMessage,
            byte channel)
        {
            if (networkConnection == null)
                return true;

            string message = chatMessage.Message;
            if (string.IsNullOrWhiteSpace(message))
                return true;

            if (message.StartsWith("/", StringComparison.Ordinal))
                return true;

            if (WasHandledThisFrame(networkConnection.ClientId, message))
                return false;

            bool handledCommand = TryHandleCommand(message, networkConnection.ClientId, isHostLocal: false);
            if (!handledCommand)
                BroadcastPlayerMessage(networkConnection.ClientId, message, chatMessage.Username);

            MarkHandled(networkConnection.ClientId, message);
            return false;
        }

        public static void BroadcastMessage(int clientId, string text, string username = "", int showAboveUser = 0)
        {
            if (clientId != 0 && TrySendLocalReply(clientId, text))
                return;

            var serverManager = InstanceFinder.ServerManager;
            if (serverManager == null)
            {
                Debug.LogError("[ChatSystem] Cannot send chat message: server manager is unavailable.");
                return;
            }

            if (clientId == 0)
            {
                serverManager.Broadcast(CreatePublicChatMessage(text, username, showAboveUser), true);
                MelonLogger.Msg($"[LobbyKit] chat | {StripRichText(username)}: {StripRichText(text)}");
                return;
            }

            if (!serverManager.Clients.TryGetValue(clientId, out NetworkConnection connection))
            {
                Debug.LogError($"[ChatSystem] Cannot send private chat message: client {clientId} was not found.");
                return;
            }

            serverManager.Broadcast(connection, CreateSystemChatMessage(text), true);
        }

        public static void BroadcastSystemMessage(string text)
        {
            var serverManager = InstanceFinder.ServerManager;
            if (serverManager == null)
            {
                Debug.LogError("[ChatSystem] Cannot send system chat message: server manager is unavailable.");
                return;
            }

            serverManager.Broadcast(CreateSystemChatMessage(text), true);
        }

        public static void SendMotdToPlayer(PlayerReference player)
        {
            if (!LobbyKitCore.isHost || player == null)
                return;

            int connectionId = player.ConnectionID;
            string motd = LobbyKitCore.MessageOfTheDay;
            if (string.IsNullOrWhiteSpace(motd) || !MotdRecipients.Add(connectionId))
                return;

            MelonLogger.Msg($"[LobbyKit] MOTD -> {StripRichText(player.Username)} (conn {connectionId})");
            MelonCoroutines.Start(SendMotdWhenReady(connectionId));
        }

        public static void ForgetMotdRecipient(int connectionId)
        {
            MotdRecipients.Remove(connectionId);
        }

        public static void ForgetTeleportRequests(int connectionId)
        {
            TeleportRequests.Remove(connectionId);

            foreach (int targetConnectionId in new List<int>(TeleportRequests.Keys))
            {
                if (TeleportRequests[targetConnectionId] == connectionId)
                    TeleportRequests.Remove(targetConnectionId);
            }
        }

        public static void ResetSessionState()
        {
            MotdRecipients.Clear();
            TeleportRequests.Clear();
            LastCommandBySource.Clear();
            LastExplicitCommandBySource.Clear();
            PendingConfirmations.Clear();
            Features.Anticheat.PlayerSizeRegistry.Clear();
        }

        private static bool TryHandleCommand(string message, int connectionId, bool isHostLocal)
        {
            string trimmedMessage = message.Trim();
            if (trimmedMessage == "!!")
            {
                if (!LastExplicitCommandBySource.TryGetValue(connectionId, out string lastCommand) || string.IsNullOrWhiteSpace(lastCommand))
                {
                    BroadcastMessage(connectionId, "<#FA0>No previous command to repeat.");
                    return true;
                }

                message = lastCommand;
                trimmedMessage = lastCommand.Trim();
            }

            string[] parts = trimmedMessage.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || !Commands.TryGetValue(parts[0], out CommandDefinition command))
                return false;

            string commandName = parts[0];
            string args = parts.Length > 1 ? parts[1].Trim() : string.Empty;
            bool isMotdCommand = commandName.Equals(MotdCommand, StringComparison.OrdinalIgnoreCase);
            PermLevel level = Perms.GetLevel(connectionId);

            // Silently hide an unset MOTD from non-admins so !motd isn't noise on a server without one.
            if (isMotdCommand && !HasMotd() && (string.IsNullOrWhiteSpace(args) || level < PermLevel.Admin))
                return true;

            // Required level: the op-file's per-command override, or the command's built-in default.
            // A configured level above Owner (3) disables the command for everyone.
            int requiredLevel = Perms.GetCommandLevel(commandName, (int)command.MinLevel);
            if ((int)level < requiredLevel)
            {
                BroadcastMessage(connectionId, requiredLevel > (int)PermLevel.Owner
                    ? "<#FA0>That command is disabled."
                    : "<#F00>You don't have permission for that command.");
                return true;
            }

            // !bc gates GUEST (Everyone-level) command use only; mods/admins/owners bypass it entirely.
            if (level == PermLevel.Everyone && !LobbyKitCore.EnableGuestBangCommands)
            {
                BroadcastMessage(connectionId, "<#FA0>Commands are disabled on this server.");
                return true;
            }

            PlayerControl playerControl = ResolvePlayerControl(connectionId, isHostLocal);
            if (playerControl == null)
            {
                BroadcastMessage(connectionId, "<#F00>Command failed: player is not ready.");
                return true;
            }

            if (!string.Equals(trimmedMessage, "!!", StringComparison.Ordinal))
                LastExplicitCommandBySource[connectionId] = trimmedMessage;

            try
            {
                command.Handler(playerControl, args);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChatSystem] Command '{parts[0]}' failed: {ex}");
                BroadcastMessage(connectionId, "<#F00>Command failed. Check the host log for details.");
            }

            return true;
        }

        private static IEnumerator SendMotdWhenReady(int connectionId)
        {
            bool isLocal = IsLocalConnectionId(connectionId);

            for (int frame = 0; frame < 300; frame++)
            {
                bool ready = isLocal
                    ? ChatManager.Instance != null
                    : InstanceFinder.ServerManager?.Clients?.ContainsKey(connectionId) == true;

                if (ready)
                {
                    string motd = LobbyKitCore.MessageOfTheDay;
                    if (string.IsNullOrWhiteSpace(motd))
                    {
                        MotdRecipients.Remove(connectionId);
                        yield break;
                    }

                    BroadcastMessage(connectionId, motd);
                    yield break;
                }

                yield return null;
            }

            MotdRecipients.Remove(connectionId);
            Debug.LogWarning($"[ChatSystem] MOTD was not sent: client {connectionId} was not ready after 300 frames.");
        }

        private static ChatMessage CreatePublicChatMessage(string text, string username, int showAboveUser)
        {
            string cleanedUsername = AutoCloseTmpRichText(username);
            return new ChatMessage
            {
                Username = string.Empty,
                UserProductId = GetPublicMessageProductId(showAboveUser),
                Message = string.IsNullOrWhiteSpace(cleanedUsername) ? text : $"{cleanedUsername}: {text}",
                MessageType = ChatMessageType.Chat,
                SystemMessageType = (SystemMessageType)(-1)
            };
        }

        // Strips all TMP/HTML-style rich-text tags (<color>, <size>, <#FA0>, etc.) for clean log output.
        public static string StripRichText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            return Regex.Replace(text, "<[^>]*>", string.Empty);
        }

        public static string AutoCloseTmpRichText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var tagRegex = new Regex(@"<(/?)([a-zA-Z#][a-zA-Z0-9#-]*)(?:=[^>]*)?>");
            var openTags = new Stack<string>();
            var selfClosing = new HashSet<string> { "br", "space", "sprite", "page" };

            foreach (Match m in tagRegex.Matches(text))
            {
                string slash = m.Groups[1].Value;
                string tag = m.Groups[2].Value.ToLowerInvariant();

                if (tag.StartsWith("#"))
                    tag = "color";

                if (selfClosing.Contains(tag))
                    continue;

                if (slash == "/")
                {
                    if (openTags.Count > 0 && openTags.Peek() == tag)
                        openTags.Pop();
                }
                else
                {
                    openTags.Push(tag);
                }
            }

            while (openTags.Count > 0)
                text += $"</{openTags.Pop()}>";

            return text;
        }

        private static void BroadcastPlayerMessage(int connectionId, string text, string fallbackUsername = "")
        {
            PlayerReference player = Utils.FindPlayerFromConnectionId(connectionId);
            string username = !string.IsNullOrWhiteSpace(player?.Username)
                ? player.Username
                : fallbackUsername;

            BroadcastMessage(0, text, AutoCloseTmpRichText(ApplyLevelPrefix(connectionId, username)), showAboveUser: connectionId);
        }

        // Prepends the sender's configurable level chat-prefix (empty string disables it for that level).
        private static string ApplyLevelPrefix(int connectionId, string username)
        {
            string prefix = LobbyKitCore.ChatPrefixFor(Perms.GetLevel(connectionId));
            return string.IsNullOrEmpty(prefix) ? username : prefix + username;
        }

        private static ChatMessage CreateSystemChatMessage(string text)
        {
            return new ChatMessage
            {
                Username = AutoCloseTmpRichText(text) + HideSystemMessageSuffixTag,
                UserProductId = string.Empty,
                Message = string.Empty,
                MessageType = ChatMessageType.System,
                SystemMessageType = (SystemMessageType)PrivateReplySystemMessageType
            };
        }

        private static string GetPublicMessageProductId(int showAboveUser)
        {
            if (showAboveUser != 0)
            {
                var player = Utils.FindPlayerFromConnectionId(showAboveUser);
                if (player != null && !string.IsNullOrWhiteSpace(player.ProductUserId))
                    return player.ProductUserId;
            }

            return Utils.FindPlayerFromConnectionId(HostConnectionId)?.ProductUserId ?? "SERVER_MESSAGE";
        }

        private static PlayerControl ResolvePlayerControl(int connectionId, bool isHostLocal)
        {
            if (isHostLocal)
            {
                PlayerControl hostPlayer = Utils.FindHostPlayer();
                if (hostPlayer != null)
                    return hostPlayer;
            }

            return Utils.FindPlayerFromConnectionId(connectionId)?.PlayerControl;
        }

        private static bool TrySendLocalReply(int clientId, string text)
        {
            if (!IsLocalConnectionId(clientId))
                return false;

            ChatManager chatManager = ChatManager.Instance;
            if (chatManager == null)
                return false;

            try
            {
                chatManager.SendLocalSystemChatMessage(text);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChatSystem] Local command reply failed: {ex}");
                return false;
            }
        }

        private static bool IsLocalConnectionId(int connectionId)
        {
            if (connectionId == HostConnectionId)
                return true;

            PlayerReference localPlayer = LobbyKitCore.Instance?.GetLocalPlayer();
            return localPlayer != null && localPlayer.ConnectionID == connectionId;
        }

        private static bool WasHandledThisFrame(int connectionId, string message)
        {
            return LastCommandBySource.TryGetValue(connectionId, out var last) &&
                   last.Frame == Time.frameCount &&
                   last.Message == message;
        }

        private static void MarkHandled(int connectionId, string message)
        {
            LastCommandBySource[connectionId] = (message, Time.frameCount);
        }

        private static void HandleHelpCommand(PlayerControl playerControl, string args)
        {
            string helpSizing = "<size=75%>";
            if (!string.IsNullOrWhiteSpace(args))
            {
                string requested = args.Trim();
                if (!requested.StartsWith("!", StringComparison.Ordinal))
                    requested = "!" + requested;

                if (Commands.TryGetValue(requested, out CommandDefinition command) && CanShowInHelp(requested, command, playerControl))
                    Reply(playerControl, $"{helpSizing}<#7FF>{FormatCommandUsage(requested, command)} - {command.Description}");
                else
                    Reply(playerControl, $"{helpSizing}<#FA0>Unknown command: {requested}");

                return;
            }

            Reply(playerControl, $"{helpSizing}<#7FF>Available commands:");
            foreach (var entry in Commands)
            {
                if (CanShowInHelp(entry.Key, entry.Value, playerControl))
                    Reply(playerControl, $"{helpSizing}<#7FF>{FormatCommandUsage(entry.Key, entry.Value)}");
            }
        }

        private static bool CanShowInHelp(string commandName, CommandDefinition command, PlayerControl playerControl)
        {
            return !command.HiddenFromHelp &&
                   (!commandName.Equals(MotdCommand, StringComparison.OrdinalIgnoreCase) || HasMotd()) &&
                   CanUseCommand(commandName, command, playerControl);
        }

        private static bool HasMotd()
        {
            return !string.IsNullOrWhiteSpace(LobbyKitCore.MessageOfTheDay);
        }

        private static bool CanUseCommand(string commandName, CommandDefinition command, PlayerControl playerControl)
        {
            if (playerControl == null) return false;
            int required = Perms.GetCommandLevel(commandName, (int)command.MinLevel);
            return (int)Perms.GetLevel(playerControl.OwnerId) >= required;
        }

        private static string FormatCommandUsage(string commandName, CommandDefinition command)
        {
            int required = Perms.GetCommandLevel(commandName, (int)command.MinLevel);
            return required > (int)PermLevel.Everyone
                ? $"{command.Usage} ({LevelLabel(required)})"
                : command.Usage;
        }

        private static string LevelLabel(int level)
            => level > (int)PermLevel.Owner ? "Disabled" : ((PermLevel)level).DisplayName();

        private static void HandleBangCommandsCommand(PlayerControl playerControl, string args)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                string state = LobbyKitCore.EnableGuestBangCommands ? "enabled" : "disabled";
                Reply(playerControl, $"<#7FF>Guest bang commands are {state}. Usage: !bangcommands <on|off>");
                return;
            }

            string value = args.Trim().ToLowerInvariant();
            bool? enabled = value switch
            {
                "on" or "enable" or "enabled" or "true" or "1" => true,
                "off" or "disable" or "disabled" or "false" or "0" => false,
                _ => null
            };

            if (!enabled.HasValue)
            {
                Reply(playerControl, "<#F00>Usage: !bangcommands <on|off>");
                return;
            }

            LobbyKitCore.SetEnableGuestBangCommands(enabled.Value);
            Reply(playerControl, $"<#FF0>Guest bang commands {(enabled.Value ? "enabled" : "disabled")}.");
        }

        private static void HandleMotdCommand(PlayerControl playerControl, string args)
        {
            if (string.IsNullOrWhiteSpace(args))   // read — anyone
            {
                Reply(playerControl, $"<#7FF>MOTD: </color>{LobbyKitCore.MessageOfTheDay}");
                return;
            }

            if (!Perms.Has(playerControl, PermLevel.Admin))   // write — admin only
            {
                Reply(playerControl, "<#F00>Only admins can set the MOTD.");
                return;
            }

            LobbyKitCore.SetMessageOfTheDay(args.Trim());
            Reply(playerControl, $"<#FF0>MOTD set: </color>{LobbyKitCore.MessageOfTheDay}");
        }


        private static void HandleSizeCommand(PlayerControl playerControl, string args)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                float current = Features.Anticheat.PlayerSizeRegistry.GetSize(playerControl.OwnerId);
                Reply(playerControl, $"<#7FF>Your size is {current:0.##}. Usage: !size <{Features.Anticheat.PlayerSizeRegistry.MinSize:0.##}-{Features.Anticheat.PlayerSizeRegistry.MaxSize:0.##}>");
                return;
            }

            if (!float.TryParse(args.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float size))
            {
                Reply(playerControl, "<#F00>Usage: !size <number>");
                return;
            }

            if (!Features.Anticheat.PlayerSizeRegistry.TrySetSize(playerControl.OwnerId, size))
            {
                Reply(playerControl, $"<#FA0>Size must be between {Features.Anticheat.PlayerSizeRegistry.MinSize:0.##} and {Features.Anticheat.PlayerSizeRegistry.MaxSize:0.##}.");
                return;
            }

            Features.Anticheat.PlayerScalePacketClamp.ApplySize(playerControl, size);
            Reply(playerControl, $"<#FF0>Size set to {size:0.##}.");
        }

        // ── Moderation & permissions ─────────────────────────────────────────────────
        // One pending confirmation per requester (by connection id). Destructive commands stage an action and
        // the requester runs !confirm to execute it.
        private static readonly Dictionary<int, (Action Action, string Description)> PendingConfirmations = new();

        internal static void RequestConfirmation(PlayerControl playerControl, string description, Action action)
        {
            PendingConfirmations[playerControl.OwnerId] = (action, description);
            Reply(playerControl, $"<#FA0>{description}? Type <#FFF>!confirm</color> to proceed (or ignore to cancel).");
        }


        private static (string First, string Remainder) SplitFirstWord(string args)
        {
            if (string.IsNullOrWhiteSpace(args)) return (string.Empty, string.Empty);
            string[] p = args.Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            return (p[0], p.Length > 1 ? p[1].Trim() : string.Empty);
        }

        private static bool LooksLikePuid(string value)
            => !string.IsNullOrWhiteSpace(value) && value.Length >= 16 && value.IndexOf(' ') < 0;

        private static void TeleportPlayerTo(PlayerControl player, PlayerControl destination)
        {
            player.RpcWriter___RpcResetPosition___3848837105(
                destination.transform.position,
                destination.transform.rotation);
        }

        private static void Reply(PlayerControl playerControl, string message)
        {
            BroadcastMessage(playerControl.OwnerId, message);
        }
    }
}

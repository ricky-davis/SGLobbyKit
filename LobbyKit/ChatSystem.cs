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
                "!help [command|page]",
                "Lists commands (paged) or details for one command.",
                hiddenFromHelp: true),
            ["!settings"] = new CommandDefinition(
                HandleSettingsCommand,
                "!settings [?|key] [value]",
                "List settings with '!settings ?', or change e.g. !settings bc on.",
                minLevel: PermLevel.Admin),
            [MotdCommand] = new CommandDefinition(
                HandleMotdCommand,
                "!motd [message]",
                "Show the message of the day. Admins can pass text to set it."),
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

        // The in-game chatbox hard-wraps at this many characters per line (including any sender prefix), and
        // we keep each help page to this many lines (a header line + command lines). Each Reply renders as one
        // chat line, so every help line must stay within HelpLineChars (else it wraps into extra lines).
        private const int HelpLineChars = 36;
        private const int HelpLinesPerPage = 8;

        private static void HandleHelpCommand(PlayerControl playerControl, string args)
        {
            string a = args?.Trim() ?? string.Empty;

            // !help <command> → detailed help for a single command (anything non-numeric is a command name).
            if (a.Length > 0 && !int.TryParse(a, out _))
            {
                string requested = a.StartsWith("!", StringComparison.Ordinal) ? a : "!" + a;
                if (Commands.TryGetValue(requested, out CommandDefinition command) && CanShowInHelp(requested, command, playerControl))
                {
                    foreach (string line in HelpWordWrap($"{FormatCommandUsage(requested, command)} - {command.Description}", HelpLineChars, HelpLinesPerPage))
                        Reply(playerControl, HelpLine(line));
                }
                else
                {
                    Reply(playerControl, HelpLine($"Unknown command: {requested}", warn: true));
                }
                return;
            }

            // List view, paginated. Each visible command is one line (usage, truncated to the line width).
            var entries = new List<string>();
            foreach (var entry in Commands)
                if (CanShowInHelp(entry.Key, entry.Value, playerControl))
                    entries.Add(HelpTruncate(FormatCommandUsage(entry.Key, entry.Value), HelpLineChars));

            if (entries.Count == 0)
            {
                Reply(playerControl, HelpLine("No commands available."));
                return;
            }

            // First line is always a header showing the page number; the rest of the page lists commands.
            int perPage = HelpLinesPerPage - 1;
            int pages = (entries.Count + perPage - 1) / perPage;

            int page = (a.Length > 0 && int.TryParse(a, out int p)) ? p : 1;
            page = Math.Max(1, Math.Min(page, pages));

            string header = $"Commands (Page {page}/{pages})";
            if (page < pages) header += $" !help {page + 1}";
            Reply(playerControl, HelpLine(HelpTruncate(header, HelpLineChars), header: true));

            int start = (page - 1) * perPage;
            int stop = Math.Min(start + perPage, entries.Count);
            for (int i = start; i < stop; i++)
                Reply(playerControl, HelpLine(entries[i]));
        }

        // One help chat line: keeps the existing small size; yellow header, blue content, amber warning.
        private static string HelpLine(string text, bool header = false, bool warn = false)
            => $"<size=75%><#{(warn ? "FA0" : header ? "FF0" : "7FF")}>{text}";

        private static string HelpTruncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
            return max <= 1 ? s.Substring(0, max) : s.Substring(0, max - 1) + "…";
        }

        // Greedy word-wrap of PLAIN text (no rich-text tags) to lines of at most maxChars, capped at maxLines.
        // A single token longer than a line is hard-trimmed with an ellipsis.
        private static List<string> HelpWordWrap(string text, int maxChars, int maxLines)
        {
            var lines = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return lines;

            var current = new System.Text.StringBuilder();
            foreach (string word in text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string w = word.Length > maxChars ? word.Substring(0, maxChars - 1) + "…" : word;
                if (current.Length == 0)
                {
                    current.Append(w);
                }
                else if (current.Length + 1 + w.Length <= maxChars)
                {
                    current.Append(' ').Append(w);
                }
                else
                {
                    lines.Add(current.ToString());
                    current.Clear();
                    if (lines.Count >= maxLines) return lines;
                    current.Append(w);
                }
            }
            if (current.Length > 0 && lines.Count < maxLines) lines.Add(current.ToString());
            return lines;
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

        // Splits "<name> [trailing...]" where <name> is an ONLINE player's name that may contain spaces,
        // quotes, or other special characters (e.g. a player literally named: "I don't know"). We can't rely
        // on whitespace to find the name boundary, and we don't use quoting (the name itself may contain
        // quotes). Instead we greedily try the longest word-prefix of args and shrink it one word at a time
        // until it EXACTLY matches a connected player's name; everything after that is the trailing remainder
        // (e.g. a kick/ban reason). Returns (null, "") when no exact-name prefix matches — callers may then
        // fall back to a fuzzy single-word match for typo tolerance.
        private static (PlayerReference Match, string Remainder) MatchLeadingPlayer(string args)
        {
            if (string.IsNullOrWhiteSpace(args)) return (null, string.Empty);
            string s = args.Trim();

            int cut = s.Length;
            while (cut > 0)
            {
                string candidate = s.Substring(0, cut);
                PlayerReference match = Utils.FindPlayerByExactName(candidate, sanitized: true);
                if (match != null)
                    return (match, s.Substring(cut).Trim());

                // Back up to the previous space so the next candidate ends on a word boundary.
                int prevSpace = s.LastIndexOf(' ', cut - 1);
                if (prevSpace < 0) break;
                cut = prevSpace;
            }

            return (null, string.Empty);
        }

        // Resolves a leading player reference from a command's args, supporting names with spaces/special
        // characters via MatchLeadingPlayer, and falling back to the legacy single-word fuzzy match (typo
        // tolerance) when no exact-name prefix matches. Returns the player (null if unresolved) and the
        // trailing remainder (e.g. a reason).
        private static (PlayerReference Target, string Remainder) ResolveLeadingPlayer(string args)
        {
            var (exact, remainder) = MatchLeadingPlayer(args);
            if (exact != null) return (exact, remainder);

            var (firstWord, rest) = SplitFirstWord(args);
            // Never fuzzy-match a PUID-looking token to a player NAME — that could ban/kick the wrong online
            // player (a long PUID can exceed the loose 0.1 similarity threshold against a short name). A real
            // player literally named like a PUID would already have matched above via the exact pass, so this
            // only suppresses the fuzzy fallback and lets callers (e.g. !ban) handle the token as a PUID.
            if (LooksLikePuid(firstWord)) return (null, rest);
            return (Utils.FindPlayerByName(firstWord, sanitized: true), rest);
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

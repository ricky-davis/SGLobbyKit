using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppFishNet;
using Il2CppFishNet.Connection;
using Il2CppDissonance.Integrations.FishNet;
using Il2Cpp_Scripts.Player;
using Il2Cpp_Scripts.Systems.Chat;
using UnityEngine;
using System.Text.RegularExpressions;

namespace MultiplayerTools.Patches
{
    [HarmonyPatch]
    public static class ChatSystem
    {
        private const int HostConnectionId = 32767;
        private const int PrivateReplySystemMessageType = 0;
        private const string HideSystemMessageSuffixTag = "<size=0>";

        private delegate void ChatCommandHandler(PlayerControl playerControl, string args);

        private sealed class CommandDefinition
        {
            public CommandDefinition(ChatCommandHandler handler, string usage, string description)
            {
                Handler = handler;
                Usage = usage;
                Description = description;
            }

            public ChatCommandHandler Handler { get; }
            public string Usage { get; }
            public string Description { get; }
        }

        private static readonly Dictionary<int, (string Message, int Frame)> LastCommandBySource = new();

        private static readonly Dictionary<string, CommandDefinition> Commands = new(StringComparer.OrdinalIgnoreCase)
        {
            ["!help"] = new CommandDefinition(
                HandleHelpCommand,
                "!help [command]",
                "Shows available commands or details for one command."),
            ["!tp"] = new CommandDefinition(
                HandleTpCommand,
                "!tp <name>",
                "Teleport to a player by name.")
        };

        [HarmonyPatch(typeof(ChatManager), "ProcessChatInput")]
        [HarmonyPrefix]
        private static bool ChatManager_ProcessChatInput_Prefix(ChatManager __instance)
        {
            var localPlayer = MultiplayerToolsCore.Instance?.GetLocalPlayer();
            if (!MultiplayerToolsCore.isHost || localPlayer == null || localPlayer.ConnectionID != HostConnectionId)
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
                BroadcastMessage(0, message, localPlayer.Username, showAboveUser: localPlayer.ConnectionID);

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
                return;
            }

            if (!serverManager.Clients.TryGetValue(clientId, out NetworkConnection connection))
            {
                Debug.LogError($"[ChatSystem] Cannot send private chat message: client {clientId} was not found.");
                return;
            }

            serverManager.Broadcast(connection, CreatePrivateReplyMessage(text), true);
        }

        private static bool TryHandleCommand(string message, int connectionId, bool isHostLocal)
        {
            string[] parts = message.Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || !Commands.TryGetValue(parts[0], out CommandDefinition command))
                return false;

            if (!MultiplayerToolsCore.EnableGuestBangCommands && connectionId != HostConnectionId)
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

            try
            {
                string args = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                command.Handler(playerControl, args);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChatSystem] Command '{parts[0]}' failed: {ex}");
                BroadcastMessage(connectionId, "<#F00>Command failed. Check the host log for details.");
            }

            return true;
        }

        private static ChatMessage CreatePublicChatMessage(string text, string username, int showAboveUser)
        {
            string cleanedUsername = AutoCloseTmpRichText(username);
            return new ChatMessage
            {
                Username = string.Empty,
                UserProductId = GetPublicMessageProductId(showAboveUser),
                Message = string.IsNullOrWhiteSpace(cleanedUsername) ? text : $"{cleanedUsername}</style>:a {text}",
                MessageType = ChatMessageType.Chat,
                SystemMessageType = (SystemMessageType)(-1)
            };
        }

        public static string AutoCloseTmpRichText(string text)
        {
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

            BroadcastMessage(0, text, username, showAboveUser: connectionId);
        }

        private static ChatMessage CreatePrivateReplyMessage(string text)
        {
            return new ChatMessage
            {
                Username = text + HideSystemMessageSuffixTag,
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

            PlayerReference localPlayer = MultiplayerToolsCore.Instance?.GetLocalPlayer();
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
            if (!string.IsNullOrWhiteSpace(args))
            {
                string requested = args.Trim();
                if (!requested.StartsWith("!", StringComparison.Ordinal))
                    requested = "!" + requested;

                if (Commands.TryGetValue(requested, out CommandDefinition command))
                    Reply(playerControl, $"<#7FF>{command.Usage} - {command.Description}");
                else
                    Reply(playerControl, $"<#FA0>Unknown command: {requested}");

                return;
            }

            Reply(playerControl, "<#7FF>Available commands:");
            foreach (CommandDefinition command in Commands.Values)
                Reply(playerControl, $"<#7FF>{command.Usage}");
        }

        private static void HandleTpCommand(PlayerControl playerControl, string args)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Reply(playerControl, "<#F00>Usage: !tp <name>");
                return;
            }

            string targetName = args.Trim();
            PlayerReference target = Utils.FindPlayerByName(targetName, sanitized: true);
            if (target == null)
            {
                Reply(playerControl, $"<#FA0>Player not found: {targetName}");
                return;
            }

            if (target.PlayerControl == null)
            {
                Reply(playerControl, $"<#FA0>Player is not ready: {target.Username}");
                return;
            }

            playerControl.RpcWriter___RpcResetPosition___3848837105(
                target.PlayerControl.transform.position,
                target.PlayerControl.transform.rotation);
            Reply(playerControl, $"<#FF0>Tp'd to {target.Username}");
        }

        private static void Reply(PlayerControl playerControl, string message)
        {
            BroadcastMessage(playerControl.OwnerId, message);
        }
    }
}

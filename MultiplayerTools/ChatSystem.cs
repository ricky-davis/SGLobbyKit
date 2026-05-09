using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppFishNet;
using Il2CppFishNet.Connection;
using Il2Cpp_Scripts.Systems.Chat;
using UnityEngine;
using Object = UnityEngine.Object;
using MelonLoader;
using Il2CppFishNet.Object.Synchronizing;
using static Il2CppRewired.Demos.CustomPlatform.MyPlatformControllerExtension;

namespace MultiplayerTools.Patches
{
    [HarmonyPatch]
    public static class ChatSystem
    {
        private static PlayerReference _fakePlayerReference;

        private struct CommandStamp
        {
            public string Message;
            public int Frame;
        }

        private static readonly Dictionary<int, CommandStamp> _lastCommandBySource = new();

        private delegate bool ChatCommandHandler(PlayerControl playerControl, string args);

        private sealed class ChatCommandDefinition
        {
            public ChatCommandHandler Handler;
            public string Usage;
            public string Description;
        }

        private static readonly Dictionary<string, ChatCommandDefinition> _commands = new(StringComparer.OrdinalIgnoreCase)
        {
            {
                "!help",
                new ChatCommandDefinition
                {
                    Handler = HandleHelpCommand,
                    Usage = "!help [command]",
                    Description = "Shows available commands or details for one command."
                }
            },
            {
                "!tp",
                new ChatCommandDefinition
                {
                    Handler = HandleTpCommand,
                    Usage = "!tp <name>",
                    Description = "Teleport to a player by name."
                }
            }
        };

        [HarmonyPatch(typeof(ChatManager), "ProcessChatInput")]
        [HarmonyPrefix]
        private static bool ChatManager_ProcessChatInput_Prefix(ChatManager __instance)
        {
            if (!MultiplayerToolsCore.isHost)
                return true;

            var chatBox = __instance.chatBox;

            string raw = chatBox.inputFieldValue;
            if (string.IsNullOrWhiteSpace(raw))
                return true;

            string cleaned = StringHelpers.PrepareStringForChat(raw);
            if (string.IsNullOrWhiteSpace(cleaned) || !cleaned.StartsWith("!"))
                return true;

            bool shouldPassThrough = HandleCommand(cleaned, -1, isHostLocal: true);
            if (shouldPassThrough)
                return true;

            chatBox.inputFieldValue = "";
            chatBox.ClearInputBox();
            return false;
        }

        [HarmonyPatch(typeof(ChatManager), "OnServerReceivedChatBroadcastFromClient")]
        [HarmonyPrefix]
        private static bool ChatManager_OnServerReceivedChatBroadcastFromClient_Prefix(NetworkConnection networkConnection, ChatMessage chatMessage, byte channel)
        {
            if (!MultiplayerToolsCore.EnableGuestBangCommands)
                return true;

            string msg = chatMessage.Message;
            if (string.IsNullOrWhiteSpace(msg) || !msg.StartsWith("!"))
                return true;

            return HandleCommand(msg, networkConnection.ClientId, isHostLocal: false);
        }


        [HarmonyPatch(typeof(PlayerReferenceManager), "OnPlayerReferenceAdded")]
        [HarmonyPostfix]
        public static void OnPlayerReferenceAdded_Postfix(
            PlayerReferenceManager __instance,
            int index)
        {
            var playerRef = __instance.sync_PlayerReferences[index];
            MelonLogger.Msg($"Player reference added: {playerRef}");

            if (playerRef == null)
                return;
            if (playerRef.ConnectionID == 32767)
            {
                Debug.Log("Adding fake player reference for server (host) player...");
                _fakePlayerReference = AddFakeServerPlayerReference();
            }
            else
            {
                Debug.Log("Not Host Player");
            }

            // You can inspect the current list here.
            MelonLogger.Msg($"Total references: {__instance.sync_PlayerReferences.Count}");
        }

        public static void BroadcastMessage(int clientId, string text)
        {
            var sm = InstanceFinder.ServerManager;

            if (sm == null)
            {
                Debug.LogError("[Chat] Cannot send message: server manager is unavailable. Are you hosting?");
                return;
            }

            var msg = new ChatMessage
            {
                Username = "",
                UserProductId = _fakePlayerReference.ProductUserId.ToString(),
                Message = text,
                MessageType = ChatMessageType.Chat,
                SystemMessageType = (SystemMessageType)(-1)
            };

            if (clientId < 0)
            {
                sm.Broadcast(msg, true);
                return;
            }

            if (!sm.Clients.TryGetValue(clientId, out NetworkConnection conn))
            {
                Debug.LogError($"[Chat] Cannot send private message: client not found (clientId={clientId}).");
                return;
            }

            sm.Broadcast(conn, msg, true);
        }

        public static PlayerReference AddFakeServerPlayerReference()
        {
            var manager = PlayerReferenceManager.Instance;

            if (manager == null || manager.sync_PlayerReferences == null)
            {
                Debug.LogError("[Chat] Cannot create fake server player: player references are not ready.");
                return null;
            }

            if (manager.sync_PlayerReferences.Count == 0)
            {
                Debug.LogError("[Chat] Cannot create fake server player: no player references available to clone.");
                return null;
            }

            var src = manager.sync_PlayerReferences[0];

            string fakeProductId = "00000000000000000000000000000000";
            string fakeVoiceId = "";
            int fakeConnectionId = 32766;
            long fakePlatformUserId = 01234567898765432L;

            if (!manager._communicationPoliciesByPlatformUserId.ContainsKey(fakePlatformUserId))
            {
                _fakePlayerReference = null;
                var srcPc = src.PlayerControl;
                var cloneGo = Object.Instantiate(srcPc.gameObject);
                cloneGo.name = "Fake Server PlayerControl";
                cloneGo.SetActive(false);

                var fakePc = cloneGo.GetComponent<PlayerControl>();

                manager.Server_AddPlayerReference(
                    fakeProductId,
                    fakePlatformUserId,
                    fakeConnectionId,
                    "",
                    fakeVoiceId,
                    src.AuthPlatform,
                    fakePc
                );
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

            return fake;
        }

        private static bool HandleCommand(string message, int connectionId, bool isHostLocal)
        {
            int frame = Time.frameCount;
            if (_lastCommandBySource.TryGetValue(connectionId, out CommandStamp last) &&
                last.Frame == frame &&
                last.Message == message)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(message))
                return true;

            message = message.Trim();

            PlayerControl pc = isHostLocal ? Utils.FindHostPlayer() : Utils.FindPlayerFromConnectionId(connectionId);
            if (pc == null)
                return true;

            string[] parts = message.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return true;

            string command = parts[0];
            string args = parts.Length > 1 ? parts[1].Trim() : string.Empty;

            if (_commands.TryGetValue(command, out ChatCommandDefinition commandDef))
            {
                _lastCommandBySource[connectionId] = new CommandStamp { Message = message, Frame = frame };
                return commandDef.Handler(pc, args);
            }

            return true;
        }

        private static bool HandleHelpCommand(PlayerControl pc, string args)
        {
            if (!string.IsNullOrWhiteSpace(args))
            {
                string requested = args.Trim();
                if (!requested.StartsWith("!", StringComparison.Ordinal))
                    requested = "!" + requested;

                if (_commands.TryGetValue(requested, out ChatCommandDefinition commandDef))
                {
                    BroadcastMessage(pc.OwnerId, $"<#7FF>{commandDef.Usage} - {commandDef.Description}");
                    return false;
                }

                BroadcastMessage(pc.OwnerId, $"<#FA0>Unknown command: {requested}");
                return false;
            }

            BroadcastMessage(pc.OwnerId, "<#7FF>Available commands:");
            foreach (var pair in _commands)
                BroadcastMessage(pc.OwnerId, $"<#7FF>{pair.Value.Usage}");

            return false;
        }

        private static bool HandleTpCommand(PlayerControl pc, string args)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                BroadcastMessage(pc.OwnerId, "<#F00>Usage: !tp <name>");
                return false;
            }

            string targetName = args.Trim();
            PlayerReference otherPlayer = Utils.FindPlayerByName(targetName);

            if (otherPlayer == null)
            {
                BroadcastMessage(pc.OwnerId, $"<#FA0>Player not found: {targetName}");
                return false;
            }

            Vector3 targetPos = otherPlayer.PlayerControl.transform.position;
            Quaternion targetRot = otherPlayer.PlayerControl.transform.rotation;

            pc.RpcWriter___RpcResetPosition___3848837105(targetPos, targetRot);
            BroadcastMessage(pc.OwnerId, $"<#FF0>Tp'd to {otherPlayer.Username}");
            return false;
        }
    }
}

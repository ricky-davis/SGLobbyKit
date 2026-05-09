using HarmonyLib;
using Il2Cpp;
using Il2CppFishNet;
using Il2CppFishNet.Connection;
using Il2Cpp_Scripts.Systems.Chat;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MultiplayerTools.Patches
{
    [HarmonyPatch]
    public static class ChatSystem
    {
        public static PlayerReference fakePlayerReference;

        private struct CommandStamp
        {
            public string Message;
            public int Frame;
        }

        private static readonly Dictionary<int, CommandStamp> _lastCommandBySource = new();

        [HarmonyPatch(typeof(ChatManager), "ProcessChatInput")]
        [HarmonyPrefix]
        private static bool ProcessChatInput_Prefix(ChatManager __instance)
        {
            var chatBox = __instance.chatBox;
            if (chatBox == null)
                return true;

            string raw = chatBox.inputFieldValue;
            if (string.IsNullOrWhiteSpace(raw))
                return true;

            string cleaned = StringHelpers.PrepareStringForChat(raw);
            if (string.IsNullOrWhiteSpace(cleaned) || !cleaned.StartsWith("!"))
                return true;

            int sourceId = -1;
            int frame = Time.frameCount;

            if (_lastCommandBySource.TryGetValue(sourceId, out CommandStamp last) &&
                last.Frame == frame &&
                last.Message == cleaned)
            {
                return false;
            }

            _lastCommandBySource[sourceId] = new CommandStamp { Message = cleaned, Frame = frame };

            chatBox.inputFieldValue = "";
            chatBox.ClearInputBox();

            HandleCommand(cleaned, null, isHostLocal: true);
            return false;
        }

        [HarmonyPatch(typeof(ChatManager), "OnServerReceivedChatBroadcastFromClient")]
        [HarmonyPrefix]
        private static bool ServerReceive_Prefix(NetworkConnection networkConnection, ChatMessage chatMessage, byte channel)
        {
            if (!InstanceFinder.IsServerStarted)
                return true;

            if (networkConnection == null || chatMessage == null)
                return true;

            string msg = chatMessage.Message;
            if (string.IsNullOrWhiteSpace(msg) || !msg.StartsWith("!"))
                return true;

            int connId = networkConnection.ClientId;
            int frame = Time.frameCount;

            if (_lastCommandBySource.TryGetValue(connId, out CommandStamp last) &&
                last.Frame == frame &&
                last.Message == msg)
            {
                return false;
            }

            _lastCommandBySource[connId] = new CommandStamp { Message = msg, Frame = frame };

            HandleCommand(msg, networkConnection, isHostLocal: false);
            return false;
        }

        public static void BroadcastMessage(int clientId, string text)
        {
            fakePlayerReference = AddFakeServerPlayerReference();
            var sm = InstanceFinder.ServerManager;

            if (sm == null)
            {
                Debug.LogError("ServerManager is null. Run this on host/server.");
                return;
            }

            var msg = new ChatMessage
            {
                Username = "",
                UserProductId = fakePlayerReference.ProductUserId.ToString(),
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
                Debug.LogError($"No client found with id {clientId}");
                return;
            }

            sm.Broadcast(conn, msg, true);
        }

        public static PlayerReference AddFakeServerPlayerReference()
        {
            var manager = PlayerReferenceManager.Instance;

            if (manager == null || manager.sync_PlayerReferences == null)
            {
                Debug.LogError("PlayerReferenceManager or sync_PlayerReferences is null.");
                return null;
            }

            if (manager.sync_PlayerReferences.Count == 0)
            {
                Debug.LogError("No PlayerReferences to clone.");
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
                var cloneGo = Object.Instantiate(srcPc.gameObject);
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

        private static bool HandleCommand(string message, NetworkConnection connection, bool isHostLocal)
        {
            if (string.IsNullOrWhiteSpace(message))
                return true;

            message = message.Trim();
            if (!message.StartsWith("!", StringComparison.Ordinal))
                return true;

            PlayerControl pc = isHostLocal ? Utils.FindHostPlayer() : Utils.FindPlayerFromConnection(connection);
            if (pc == null)
                return false;

            string[] parts = message.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length > 0 && parts[0].Equals("!tp", StringComparison.OrdinalIgnoreCase))
            {
                if (parts.Length < 2)
                {
                    BroadcastMessage(pc.OwnerId, "<#F00>Usage: !tp <name>");
                    return false;
                }

                string targetName = parts[1].Trim();
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

            return true;
        }
    }
}

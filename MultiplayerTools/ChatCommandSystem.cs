using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppFishNet;
using Il2CppFishNet.Connection;
using Il2Cpp_Scripts.Systems.Chat;
using UnityEngine;

namespace MultiplayerTools.Patches
{
    [HarmonyPatch]
    public static class ServerBangCommandPatch
    {
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
                    MultiplayerTools.BroadcastMessage(pc.OwnerId, "<#F00>Usage: !tp <name>");
                    return false;
                }

                string targetName = parts[1].Trim();
                PlayerReference otherPlayer = Utils.FindPlayerByName(targetName);

                if (otherPlayer == null)
                {
                    MultiplayerTools.BroadcastMessage(pc.OwnerId, $"<#FA0>Player not found: {targetName}");
                    return false;
                }

                Vector3 targetPos = otherPlayer.PlayerControl.transform.position;
                Quaternion targetRot = otherPlayer.PlayerControl.transform.rotation;

                pc.RpcWriter___RpcResetPosition___3848837105(targetPos, targetRot);
                MultiplayerTools.BroadcastMessage(pc.OwnerId, $"<#FF0>Tp'd to {otherPlayer.Username}");
                return false;
            }

            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppFishNet;
using Il2CppFishNet.Connection;
using Il2Cpp_Scripts.Managers;
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

            PlayerControl pc = isHostLocal ? FindHostPlayer() : FindPlayerFromConnection(connection);
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
                PlayerReference otherPlayer = FindPlayerByName(targetName);

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

        private static PlayerControl FindHostPlayer()
        {
            var players = UnityEngine.Object.FindObjectsOfType<PlayerControl>();
            foreach (var pc in players)
            {
                if (pc != null && pc.IsOwner)
                    return pc;
            }

            return UnityEngine.Object.FindObjectOfType<PlayerControl>();
        }

        private static PlayerReference FindPlayerByName(string name, float similarityThreshold = 0.1f)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            var manager = PlayerReferenceManager.Instance;
            if (manager == null || manager.sync_PlayerReferences == null)
                return null;

            PlayerReference bestMatch = null;
            float bestScore = 0f;

            for (int i = 0; i < manager.sync_PlayerReferences.Count; i++)
            {
                PlayerReference playerRef = manager.sync_PlayerReferences[i];
                string username = playerRef.Username;
                if (string.IsNullOrWhiteSpace(username))
                    continue;

                if (username.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return playerRef;

                float score = CalculateSimilarity(username, name);
                if (score > bestScore && score >= similarityThreshold)
                {
                    bestScore = score;
                    bestMatch = playerRef;
                }
            }

            return bestMatch;
        }

        private static float CalculateSimilarity(string a, string b)
        {
            int distance = LevenshteinDistance(a.ToLowerInvariant(), b.ToLowerInvariant());
            int maxLen = Math.Max(a.Length, b.Length);
            if (maxLen == 0) return 1f;
            return 1f - (float)distance / maxLen;
        }

        private static int LevenshteinDistance(string s, string t)
        {
            int n = s.Length, m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            if (n == 0) return m;
            if (m == 0) return n;

            for (int i = 0; i <= n; d[i, 0] = i++) { }
            for (int j = 0; j <= m; d[0, j] = j++) { }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            return d[n, m];
        }

        private static PlayerControl FindPlayerFromConnection(NetworkConnection connection)
        {
            if (connection == null)
                return null;

            int clientId = connection.ClientId;
            var manager = PlayerReferenceManager.Instance;
            if (manager == null || manager.sync_PlayerReferences == null)
                return null;

            for (int i = 0; i < manager.sync_PlayerReferences.Count; i++)
            {
                PlayerReference playerRef = manager.sync_PlayerReferences[i];
                if (playerRef.ConnectionID == clientId)
                    return playerRef.PlayerControl;
            }

            return null;
        }
    }
}

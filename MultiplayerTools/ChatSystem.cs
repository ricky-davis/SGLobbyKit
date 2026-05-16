using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using Il2CppFishNet;
using Il2CppFishNet.Connection;
using Il2Cpp_Scripts.Systems.Chat;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;
using MelonLoader;
using Il2CppFishNet.Object.Synchronizing;
using static Il2CppRewired.Demos.CustomPlatform.MyPlatformControllerExtension;
using Unity.Networking.Transport;
using Il2CppFishNet.Transporting.FishyEOSPlugin;
using static Il2CppTMPro.SpriteAssetUtilities.TexturePacker_JsonArray;
using Il2CppDissonance.Integrations.FishNet;
using UnityEngine.Rendering.RenderGraphModule.NativeRenderPassCompiler;
using Il2Cpp_Scripts.Player;

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
            },
            {
                "!name",
                new ChatCommandDefinition
                {
                    Handler = HandleNameCommand,
                    Usage = "!name <name>",
                    Description = "Set your name."
                }
            },
            {
                "!fake",
                new ChatCommandDefinition
                {
                    Handler = HandleFakePlayerCommand,
                    Usage = "!fake",
                    Description = "Spawn the fake player."
                }
            }
        };

        [HarmonyPatch(typeof(ChatManager), "ProcessChatInput")]
        [HarmonyPrefix]
        private static bool ChatManager_ProcessChatInput_Prefix(ChatManager __instance)
        {
            PlayerReference localPlayer = MultiplayerToolsCore.Instance.GetLocalPlayer();
            if (!MultiplayerToolsCore.isHost || localPlayer.ConnectionID != 32767)
                return true;
            var chatBox = __instance.chatBox;
            string raw = chatBox.InputField.text;
            if (raw.StartsWith("/"))
                return true;
            
            Debug.Log($"Local: Somehow making it here: {raw}");

            string cleaned = StringHelpers.PrepareStringForChat(raw);
            if (string.IsNullOrWhiteSpace(cleaned))
                return true;

            int frame = Time.frameCount;
            if (_lastCommandBySource.TryGetValue(localPlayer.ConnectionID, out CommandStamp last) &&
                last.Frame == frame &&
                last.Message == cleaned)
            {
                Debug.Log("Deduplicating message from client " + localPlayer.ConnectionID + ": " + raw);
                return false;
            }

            chatBox.inputFieldValue = "";
            chatBox.ClearInputBox();
            bool shouldPassThrough = HandleCommand(cleaned, localPlayer.ConnectionID, isHostLocal: true);
            if (!shouldPassThrough)
            {
                _lastCommandBySource[localPlayer.ConnectionID] = new CommandStamp { Message = cleaned, Frame = frame };
                return false;
            }

            BroadcastMessage(0, $"</color></color></color></color>1{localPlayer.Username}</color>: {cleaned}", showAboveUser: localPlayer.ConnectionID);
            _lastCommandBySource[localPlayer.ConnectionID] = new CommandStamp { Message = cleaned, Frame = frame };
            return false;
        }

        [HarmonyPatch(typeof(ChatManager), "OnServerReceivedChatBroadcastFromClient")]
        [HarmonyPrefix]
        private static bool ChatManager_OnServerReceivedChatBroadcastFromClient_Prefix(Il2CppFishNet.Connection.NetworkConnection networkConnection, ChatMessage chatMessage, byte channel)
        {
            string msg = chatMessage.Message;
            //if (msg.StartsWith("/"))
            //    return true;
            Debug.Log($"Remote: Somehow making it here: {msg}");
            if (string.IsNullOrWhiteSpace(msg))
                return true;

            int frame = Time.frameCount;
            if (_lastCommandBySource.TryGetValue(networkConnection.ClientId, out CommandStamp last) &&
                last.Frame == frame &&
                last.Message == msg)
            {
                Debug.Log("Deduplicating message from client " + networkConnection.ClientId + ": " + msg);
                return false;
            }
            bool shouldPassThrough = HandleCommand(msg, networkConnection.ClientId, isHostLocal: false);
            if (!shouldPassThrough)
            {

                _lastCommandBySource[networkConnection.ClientId] = new CommandStamp { Message = msg, Frame = frame };
                return false;
            }
            PlayerReference pr = Utils.FindPlayerFromConnectionId(networkConnection.ClientId);
            BroadcastMessage(0, $"2{pr.Username}</color>: {msg}", showAboveUser: networkConnection.ClientId);
            _lastCommandBySource[networkConnection.ClientId] = new CommandStamp { Message = msg, Frame = frame };

            return false;
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
                //_fakePlayerReference = AddFakeServerPlayerReference();
            }
            else
            {
                Debug.Log("Not Host Player");
            }

            // You can inspect the current list here.
            MelonLogger.Msg($"Total references: {__instance.sync_PlayerReferences.Count}");
        }

        public static void BroadcastMessage(int clientId, string text, string username = "", int showAboveUser = 0)
        {
            var sm = InstanceFinder.ServerManager;

            if (sm == null)
            {
                Debug.LogError("[Chat] Cannot send message: server manager is unavailable. Are you hosting?");
                return;
            }
            PlayerReference pr = Utils.FindPlayerFromConnectionId(showAboveUser);

            var msg = new ChatMessage
            {
                Username = username,
                UserProductId = showAboveUser == 0 ? _fakePlayerReference.ProductUserId.ToString() : pr.ProductUserId,
                Message = text,
                MessageType = ChatMessageType.Chat,
                SystemMessageType = (SystemMessageType)(-1)
            };

            if (clientId == 0)
            {
                sm.Broadcast(msg, true);
                return;
            }

            if (!sm.Clients.TryGetValue(clientId, out Il2CppFishNet.Connection.NetworkConnection conn))
            {
                Debug.LogError($"[Chat] Cannot send private message: client not found (clientId={clientId}).");
                return;
            }
            Debug.Log($"Broadcasting message: {msg} to {conn}");
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
                var srcGo = srcPc.gameObject;
                var wasActive = srcGo.activeSelf;

                srcGo.SetActive(false);
                var cloneGo = UnityEngine.Object.Instantiate(srcGo);
                srcGo.SetActive(wasActive);

                // Reconfigure NetworkObject so clone syncs to clients with unique identity.
                var netObj = cloneGo.GetComponent<Il2CppFishNet.Object.NetworkObject>();
                if (netObj != null)
                {
                    // ObjectId is Int32. Use a high value to avoid conflicts with normal spawned objects.
                    int uniqueObjectId = 32766; // High value to avoid normal spawn conflicts
                    AccessTools.Property(typeof(Il2CppFishNet.Object.NetworkObject), "ObjectId")
                        ?.SetValue(netObj, uniqueObjectId);

                    // Ensure it's marked as networked via the property setter (not just the field).
                    AccessTools.Property(typeof(Il2CppFishNet.Object.NetworkObject), "IsNetworked")
                        ?.SetValue(netObj, true);

                    // Clear scene ID (it's UInt64, not int). 0 = dynamically spawned.
                    AccessTools.Property(typeof(Il2CppFishNet.Object.NetworkObject), "SceneId")
                        ?.SetValue(netObj, (ulong)0);

                    // Make sure it's recognized by the network manager.
                    netObj.enabled = true;

                    // Invoke start callbacks to initialize the NetworkObject for server/client roles.
                    var invokeStartMethod = AccessTools.Method(typeof(Il2CppFishNet.Object.NetworkObject), "InvokeStartCallbacks", new[] { typeof(bool), typeof(bool) });
                    if (invokeStartMethod != null)
                    {
                        invokeStartMethod.Invoke(netObj, new object[] { true, true }); // asServer=true, invokeSyncTypeCallbacks=true
                    }
                }

                cloneGo.name = "Fake Server PlayerControl";
                cloneGo.transform.position = new Vector3(0f, -9999f, 0f);

                DisableIfExists<PlayerMovement>(cloneGo);
                DisableIfExists<PlayerSledController>(cloneGo);
                DisableIfExists<PlayerCameraControl>(cloneGo);
                DisableIfExists<PlayerAnimationController>(cloneGo);
                DisableIfExists<PlayerPushingController>(cloneGo);
                DisableIfExists<PlayerHoldingController>(cloneGo);
                DisableIfExists<PlayerTeleportationController>(cloneGo);
                DisableIfExists<HostControls>(cloneGo);
                DisableIfExists<Il2CppDissonance.Integrations.FishNet.DissonanceFishNetPlayer>(cloneGo);

                // Keep colliders and rigidbodies active for ragdoll physics to work.
                // Only reset physics state, don't disable components.
                foreach (var rb in cloneGo.GetComponentsInChildren<Rigidbody>(true))
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.detectCollisions = true;
                    // Keep isKinematic=false so ragdoll physics works
                }

                foreach (var renderer in cloneGo.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = false;

                foreach (var canvas in cloneGo.GetComponentsInChildren<Canvas>(true))
                    canvas.enabled = false;

                foreach (var audio in cloneGo.GetComponentsInChildren<AudioSource>(true))
                    audio.enabled = false;


                var fakePc = cloneGo.GetComponent<PlayerControl>();
                if (srcPc.characterModels != null && srcPc.characterModels.Count > 0)
                {
                    fakePc.characterModels.Clear();
                    foreach (var kvp in srcPc.characterModels)
                    {
                        fakePc.characterModels[kvp.Key] = kvp.Value;
                    }
                }
                cloneGo.SetActive(false);

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
        private static void DisableIfExists<T>(GameObject root) where T : Behaviour
        {
            foreach (var c in root.GetComponentsInChildren<T>(true))
                c.enabled = false;
        }
        private static bool HandleCommand(string message, int connectionId, bool isHostLocal)
        {

            if (!MultiplayerToolsCore.EnableGuestBangCommands && connectionId != 32767)
                return true;

            if (string.IsNullOrWhiteSpace(message))
                return true;

            message = message.Trim();

            PlayerControl pc = isHostLocal ? Utils.FindHostPlayer() : Utils.FindPlayerFromConnectionId(connectionId).PlayerControl;
            if (pc == null)
                return true;

            string[] parts = message.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return true;

            string command = parts[0];
            string args = parts.Length > 1 ? parts[1].Trim() : string.Empty;

            if (_commands.TryGetValue(command, out ChatCommandDefinition commandDef))
            {
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
            PlayerReference otherPlayer = Utils.FindPlayerByName(targetName, sanitized: true);

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
        private static bool HandleNameCommand(PlayerControl pc, string args)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                BroadcastMessage(pc.OwnerId, "<#F00>Usage: !name <name>");
                return false;
            }
            string newName = args.Trim();

            DissonanceFishNetPlayer voicePlayer = Utils.GetDissonancePlayer(pc);

            AccessTools
                    .Method(
                        typeof(DissonanceFishNetPlayer),
                        "RpcLogic___ServerRpcSetPlayerName___3615296227")
                    .Invoke(voicePlayer, new object[] { newName });

            BroadcastMessage(pc.OwnerId, $"<#FF0>Set name to {newName}");
            return false;
        }
        private static bool HandleFakePlayerCommand(PlayerControl pc, string args)
        {
            _fakePlayerReference = AddFakeServerPlayerReference();

            BroadcastMessage(pc.OwnerId, $"<#FF0>Spawned fake player.");
            return false;
        }
    }
}

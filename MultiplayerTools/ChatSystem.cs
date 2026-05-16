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
            },
            {
                "!test",
                new ChatCommandDefinition
                {
                    Handler = HandleTestCommand,
                    Usage = "!test",
                    Description = "Log the stats."
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
                var cloneGo = UnityEngine.Object.Instantiate(srcPc.gameObject);
                DisableIfExists<PlayerMovement>(cloneGo);
                DisableIfExists<PlayerSledController>(cloneGo);
                DisableIfExists<PlayerCameraControl>(cloneGo);
                DisableIfExists<PlayerAnimationController>(cloneGo);
                DisableIfExists<PlayerPushingController>(cloneGo);
                DisableIfExists<PlayerHoldingController>(cloneGo);
                DisableIfExists<PlayerTeleportationController>(cloneGo);
                DisableIfExists<HostControls>(cloneGo);
                DisableIfExists<Il2CppDissonance.Integrations.FishNet.DissonanceFishNetPlayer>(cloneGo);

                cloneGo.name = "Fake Server PlayerControl";
                cloneGo.transform.position = new Vector3(0f, -9999f, 0f);

                // Disable physics on the clone.
                foreach (var rb in cloneGo.GetComponentsInChildren<Rigidbody>(true))
                {
                    rb.isKinematic = true;
                    rb.detectCollisions = false;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                foreach (var col in cloneGo.GetComponentsInChildren<Collider>(true))
                {
                    col.enabled = false;
                }

                // Disable ragdoll controller on the clone.
                foreach (var ragdoll in cloneGo.GetComponentsInChildren<PlayerRagdollRigidbodyController>(true))
                {
                    ragdoll.enabled = false;
                }

                // Disable visuals/nameplate/audio sources if they exist on player clone.
                foreach (var renderer in cloneGo.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = false;

                foreach (var canvas in cloneGo.GetComponentsInChildren<Canvas>(true))
                    canvas.enabled = false;

                foreach (var audio in cloneGo.GetComponentsInChildren<AudioSource>(true))
                    audio.enabled = false;

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
        private static bool HandleTestCommand(PlayerControl pc, string args)
        {
            if (pc == null)
            {
                Debug.LogError("[Chat] Cannot export PlayerControl: player control is null.");
                return false;
            }

            GameObject rootObject = pc.transform.parent != null ? pc.transform.parent.gameObject : pc.gameObject;
            string exportPath = ExportPlayerControlHierarchy(rootObject);

            BroadcastMessage(pc.OwnerId, $"<#FF0>Exported {rootObject.name} to {Path.GetFileName(exportPath)}");
            return false;
        }

        private static string ExportPlayerControlHierarchy(GameObject rootObject)
        {
            string exportDirectory = Path.Combine(Application.persistentDataPath, "PlayerControlExports");
            Directory.CreateDirectory(exportDirectory);

            string fileName = $"PC_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            string exportPath = Path.Combine(exportDirectory, fileName);

            var builder = new StringBuilder(64 * 1024);
            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);

            builder.AppendLine($"Export time: {DateTime.Now:O}");
            builder.AppendLine($"Root object: {GetGameObjectPath(rootObject)}");
            builder.AppendLine();

            DumpGameObject(rootObject, builder, 0, visited);

            File.WriteAllText(exportPath, builder.ToString());
            Debug.Log($"[Chat] Exported PlayerControl hierarchy to {exportPath}");
            return exportPath;
        }

        private static void DumpGameObject(GameObject gameObject, StringBuilder builder, int indentLevel, HashSet<object> visited)
        {
            if (gameObject == null)
                return;

            string indent = GetIndent(indentLevel);
            builder.AppendLine($"{indent}GameObject: {gameObject.name}");
            builder.AppendLine($"{indent}  Path: {GetGameObjectPath(gameObject)}");
            builder.AppendLine($"{indent}  ActiveSelf: {gameObject.activeSelf}");
            builder.AppendLine($"{indent}  ActiveInHierarchy: {gameObject.activeInHierarchy}");
            builder.AppendLine($"{indent}  Layer: {gameObject.layer}");
            builder.AppendLine($"{indent}  Tag: {gameObject.tag}");
            builder.AppendLine($"{indent}  Transform: localPos={gameObject.transform.localPosition}, localRot={gameObject.transform.localRotation}, localScale={gameObject.transform.localScale}");

            Component[] components = gameObject.GetComponents<Component>();
            builder.AppendLine($"{indent}  Components: {components.Length}");

            for (int i = 0; i < components.Length; i++)
            {
                DumpComponent(components[i], builder, indentLevel + 1, visited);
            }

            Transform transform = gameObject.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                DumpGameObject(transform.GetChild(i).gameObject, builder, indentLevel + 1, visited);
            }
        }

        private static void DumpComponent(Component component, StringBuilder builder, int indentLevel, HashSet<object> visited)
        {
            string indent = GetIndent(indentLevel);

            if (component == null)
            {
                builder.AppendLine($"{indent}Component: <missing>");
                return;
            }

            Type type = component.GetType();
            builder.AppendLine($"{indent}Component: {type.FullName}");
            DumpObjectMembers(component, builder, indentLevel + 1, visited, 0);
        }

        private static void DumpObjectMembers(object obj, StringBuilder builder, int indentLevel, HashSet<object> visited, int depth)
        {
            if (obj == null)
                return;

            if (visited.Contains(obj))
            {
                builder.AppendLine($"{GetIndent(indentLevel)}<circular reference: {obj.GetType().FullName}>");
                return;
            }

            if (depth > 2)
            {
                builder.AppendLine($"{GetIndent(indentLevel)}<max depth reached: {obj.GetType().FullName}>");
                return;
            }

            visited.Add(obj);

            Type type = obj.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (FieldInfo field in type.GetFields(flags))
            {
                if (field.IsStatic)
                    continue;

                object value;
                try
                {
                    value = field.GetValue(obj);
                }
                catch (Exception ex)
                {
                    builder.AppendLine($"{GetIndent(indentLevel)}Field {field.Name}: <error: {ex.GetType().Name}> {ex.Message}");
                    continue;
                }

                AppendValue(builder, indentLevel, $"Field {field.Name}", value, visited, depth + 1);
            }

            foreach (PropertyInfo property in type.GetProperties(flags))
            {
                if (!property.CanRead || property.GetIndexParameters().Length != 0)
                    continue;

                object value;
                try
                {
                    value = property.GetValue(obj, null);
                }
                catch (Exception ex)
                {
                    builder.AppendLine($"{GetIndent(indentLevel)}Property {property.Name}: <error: {ex.GetType().Name}> {ex.Message}");
                    continue;
                }

                AppendValue(builder, indentLevel, $"Property {property.Name}", value, visited, depth + 1);
            }
        }

        private static void AppendValue(StringBuilder builder, int indentLevel, string label, object value, HashSet<object> visited, int depth)
        {
            string indent = GetIndent(indentLevel);

            if (value == null)
            {
                builder.AppendLine($"{indent}{label}: null");
                return;
            }

            if (value is string || value is char || value.GetType().IsPrimitive || value is decimal || value is Enum)
            {
                builder.AppendLine($"{indent}{label}: {value}");
                return;
            }

            if (value is UnityEngine.Object unityObject)
            {
                builder.AppendLine($"{indent}{label}: {unityObject.GetType().FullName} \"{unityObject.name}\"");
                return;
            }

            if (value is IEnumerable enumerable && value is not string)
            {
                builder.AppendLine($"{indent}{label}: {value.GetType().FullName}");

                int index = 0;
                foreach (object item in enumerable)
                {
                    if (index >= 64)
                    {
                        builder.AppendLine($"{GetIndent(indentLevel + 1)}[{index}]: <truncated>");
                        break;
                    }

                    AppendValue(builder, indentLevel + 1, $"[{index}]", item, visited, depth + 1);
                    index++;
                }

                return;
            }

            if (depth > 2)
            {
                builder.AppendLine($"{indent}{label}: {value.GetType().FullName}");
                return;
            }

            builder.AppendLine($"{indent}{label}: {value.GetType().FullName}");
            DumpObjectMembers(value, builder, indentLevel + 1, visited, depth);
        }

        private static string GetGameObjectPath(GameObject gameObject)
        {
            if (gameObject == null)
                return string.Empty;

            var names = new List<string>();
            Transform current = gameObject.transform;

            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private static string GetIndent(int indentLevel)
        {
            return new string(' ', Math.Max(0, indentLevel) * 2);
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return obj == null ? 0 : RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}

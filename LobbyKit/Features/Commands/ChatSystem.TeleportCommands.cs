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
    // Teleport commands: !tp / !tpme / !tpa / !tpf.
    public static partial class ChatSystem
    {
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

            string targetUsername = AutoCloseTmpRichText(target.Username);
            if (target.PlayerControl == null)
            {
                return;
            }

            TeleportPlayerTo(playerControl, target.PlayerControl);
            Reply(playerControl, $"<#FF0>TP'd to {targetUsername}");
        }

        private static void HandleTpMeCommand(PlayerControl playerControl, string args)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Reply(playerControl, "<#F00>Usage: !tpme <name>");
                return;
            }

            PlayerReference requester = Utils.FindPlayerFromConnectionId(playerControl.OwnerId);
            if (requester == null)
            {
                Reply(playerControl, "<#F00>Command failed: player is not ready.");
                return;
            }

            PlayerReference target = Utils.FindPlayerByName(args.Trim(), sanitized: true);
            if (target == null)
            {
                Reply(playerControl, $"<#FA0>Player not found: {args.Trim()}");
                return;
            }

            if (target.ConnectionID == requester.ConnectionID)
            {
                Reply(playerControl, "<#FA0>You cannot request yourself.");
                return;
            }

            string requesterUsername = AutoCloseTmpRichText(requester.Username);
            string targetUsername = AutoCloseTmpRichText(target.Username);
            TeleportRequests[target.ConnectionID] = requester.ConnectionID;
            BroadcastMessage(target.ConnectionID, $"<#7FF>{requesterUsername} wants you to TP to them. Type !tpa to accept.");
            Reply(playerControl, $"<#FF0>TP request sent to {targetUsername}.");
        }

        private static void HandleTpAcceptCommand(PlayerControl playerControl, string args)
        {
            int targetConnectionId = playerControl.OwnerId;
            if (!TeleportRequests.TryGetValue(targetConnectionId, out int requesterConnectionId))
            {
                Reply(playerControl, "<#FA0>You have no pending TP request.");
                return;
            }

            TeleportRequests.Remove(targetConnectionId);

            PlayerReference requester = Utils.FindPlayerFromConnectionId(requesterConnectionId);
            if (requester?.PlayerControl == null)
            {
                return;
            }

            string requesterUsername = AutoCloseTmpRichText(requester.Username);
            TeleportPlayerTo(playerControl, requester.PlayerControl);
            Reply(playerControl, $"<#FF0>TP'd to {requesterUsername}.");
            string accepterName = AutoCloseTmpRichText(Utils.FindPlayerFromConnectionId(targetConnectionId)?.Username ?? "A player");
            BroadcastMessage(requester.ConnectionID, $"<#FF0>{accepterName} accepted your TP request.");
        }

        private static void HandleTpForceCommand(PlayerControl playerControl, string args)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Reply(playerControl, "<#F00>Usage: !tpf <name>");
                return;
            }

            PlayerReference target = Utils.FindPlayerByName(args.Trim(), sanitized: true);
            if (target == null)
            {
                Reply(playerControl, $"<#FA0>Player not found: {args.Trim()}");
                return;
            }

            string targetUsername = AutoCloseTmpRichText(target.Username);
            if (target.PlayerControl == null)
            {
                return;
            }

            if (target.ConnectionID == playerControl.OwnerId)
            {
                Reply(playerControl, "<#FA0>You cannot force TP yourself.");
                return;
            }

            // Announce who forced the teleport (the command runner), not a generic "the host".
            string actorUsername = AutoCloseTmpRichText(
                Utils.FindPlayerFromConnectionId(playerControl.OwnerId)?.Username ?? "The host");

            TeleportPlayerTo(target.PlayerControl, playerControl);
            Reply(playerControl, $"<#FF0>Forced {targetUsername} to TP to you.");
            BroadcastMessage(target.ConnectionID, $"<#FF0>{actorUsername} TP'd you to them.");
        }
    }
}

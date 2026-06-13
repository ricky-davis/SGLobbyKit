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
                Reply(playerControl, "<#F00>Usage: !tpf <name | @all | @mods | @admins | @owners>");
                return;
            }

            // Group targets: @all teleports everyone, @mods/@admins/@owners teleport that permission level.
            if (args.TrimStart().StartsWith("@"))
            {
                HandleTpForceGroup(playerControl, args.Trim());
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

        // !tpf group targets: @all, @mods, @admins, @owners (singular forms accepted). Teleports every
        // matching connected player to the command runner. @mods/@admins/@owners match that permission
        // level EXACTLY (so "@mods" doesn't also yank admins/owners); @all matches everyone. Always
        // skipped: the runner themself, the host's own player (conn 32767 — on a headless server that's
        // the seated fake client), and references without a live PlayerControl.
        private static void HandleTpForceGroup(PlayerControl playerControl, string groupArg)
        {
            bool all = false;
            PermLevel level = PermLevel.Everyone;
            switch (groupArg.ToLowerInvariant())
            {
                case "@all": case "@everyone": all = true; break;
                case "@mod": case "@mods": level = PermLevel.Mod; break;
                case "@admin": case "@admins": level = PermLevel.Admin; break;
                case "@owner": case "@owners": level = PermLevel.Owner; break;
                default:
                    Reply(playerControl, $"<#F00>Unknown group '{groupArg}'. Use @all, @mods, @admins or @owners.");
                    return;
            }

            var manager = PlayerReferenceManager.Instance;
            if (manager == null || manager.sync_PlayerReferences == null)
            {
                Reply(playerControl, "<#F00>Command failed: player list is not ready.");
                return;
            }

            // Announce who forced the teleport (the command runner), not a generic "the host".
            string actorUsername = AutoCloseTmpRichText(
                Utils.FindPlayerFromConnectionId(playerControl.OwnerId)?.Username ?? "The host");

            int moved = 0;
            for (int i = 0; i < manager.sync_PlayerReferences.Count; i++)
            {
                PlayerReference playerRef = null;
                try { playerRef = manager.sync_PlayerReferences[i]; } catch { }
                if (playerRef == null || playerRef.PlayerControl == null)
                    continue;

                int conn = playerRef.ConnectionID;
                if (conn == playerControl.OwnerId) continue;   // never yank the runner to themself
                if (conn == 32767 || conn < 0) continue;       // never yank the host's own player
                if (!all && Perms.GetLevel(conn) != level) continue;

                try
                {
                    TeleportPlayerTo(playerRef.PlayerControl, playerControl);
                    BroadcastMessage(conn, $"<#FF0>{actorUsername} TP'd you to them.");
                    moved++;
                }
                catch { /* one bad target must not abort the group sweep */ }
            }

            string group = groupArg.ToLowerInvariant();
            Reply(playerControl, moved > 0
                ? $"<#FF0>Forced {moved} player{(moved == 1 ? "" : "s")} ({group}) to TP to you."
                : $"<#FA0>No players matched {group}.");
        }
    }
}

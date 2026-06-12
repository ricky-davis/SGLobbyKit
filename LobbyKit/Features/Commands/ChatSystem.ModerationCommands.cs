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
    // Moderation & permission commands: !confirm / !kick / !ban / !unban / !op / !level.
    public static partial class ChatSystem
    {
        private static void HandleConfirmCommand(PlayerControl playerControl, string args)
        {
            if (!PendingConfirmations.TryGetValue(playerControl.OwnerId, out var pending))
            {
                Reply(playerControl, "<#FA0>Nothing to confirm.");
                return;
            }
            PendingConfirmations.Remove(playerControl.OwnerId);
            try { pending.Action(); }
            catch (Exception ex) { Reply(playerControl, $"<#F00>Action failed: {ex.Message}"); }
        }

        private static void HandleKickCommand(PlayerControl playerControl, string args)
        {
            if (string.IsNullOrWhiteSpace(args)) { Reply(playerControl, "<#F00>Usage: !kick <name> [reason]"); return; }

            // Resolve the target by name — exact match first (so names with spaces/quotes/special chars
            // resolve precisely and the rest becomes the reason), then fuzzy for typos. See ResolveLeadingPlayer.
            (PlayerReference target, string reason) = ResolveLeadingPlayer(args);
            if (target == null) { Reply(playerControl, $"<#FA0>Player not found: {args.Trim()}"); return; }
            if (target.ConnectionID == playerControl.OwnerId) { Reply(playerControl, "<#FA0>You can't kick yourself."); return; }
            if (Perms.GetLevel(target.ConnectionID) >= Perms.GetLevel(playerControl.OwnerId))
            { Reply(playerControl, "<#FA0>You can't kick someone of equal or higher level."); return; }

            string display = AutoCloseTmpRichText(target.Username);
            int conn = target.ConnectionID;
            string reasonClean = string.IsNullOrWhiteSpace(reason) ? null : StripRichText(reason);
            RequestConfirmation(playerControl, $"Kick {display}{(reasonClean != null ? $" ({reasonClean})" : string.Empty)}", () =>
            {
                Features.Anticheat.KickAnnouncer.Enqueue(conn, reasonClean == null ? "was kicked" : $"was kicked: {reasonClean}");
                InstanceFinder.ServerManager?.Kick(conn, KickReason.Unset);
                Reply(playerControl, $"<#FF0>Kicked {display}.");
            });
        }

        private static void HandleBanCommand(PlayerControl playerControl, string args)
        {
            if (string.IsNullOrWhiteSpace(args)) { Reply(playerControl, "<#F00>Usage: !ban <name|puid> [reason]"); return; }

            // Resolve an online player by name — exact match first (names with spaces/quotes/special chars),
            // then fuzzy for typos; the rest is the reason. A PUID token never fuzzy-matches a name (see
            // ResolveLeadingPlayer), so if nothing resolves we treat the first token as a PUID (offline ban).
            (PlayerReference target, string reason) = ResolveLeadingPlayer(args);
            string puid = null; int conn = -1; string targetName = null; string display = args.Trim();

            if (target != null) { puid = target.ProductUserId; conn = target.ConnectionID; targetName = StripRichText(target.Username); display = AutoCloseTmpRichText(target.Username); }
            else
            {
                string firstToken = SplitFirstWord(args).First;
                if (LooksLikePuid(firstToken)) { puid = firstToken; display = firstToken; }
            }

            if (string.IsNullOrWhiteSpace(puid)) { Reply(playerControl, $"<#FA0>Player not found: {args.Trim()}. Pass an online name or a PUID."); return; }
            if (conn == playerControl.OwnerId) { Reply(playerControl, "<#FA0>You can't ban yourself."); return; }
            if (conn >= 0 && Perms.GetLevel(conn) >= Perms.GetLevel(playerControl.OwnerId))
            { Reply(playerControl, "<#FA0>You can't ban someone of equal or higher level."); return; }

            string reasonClean = string.IsNullOrWhiteSpace(reason) ? null : StripRichText(reason);
            string p = puid; int c = conn; string n = targetName;
            RequestConfirmation(playerControl, $"Ban {display}{(reasonClean != null ? $" ({reasonClean})" : string.Empty)}", () =>
            {
                Perms.Ban(p, reasonClean, n);
                if (c >= 0)
                {
                    Features.Anticheat.KickAnnouncer.Enqueue(c, reasonClean == null ? "was banned" : $"was banned: {reasonClean}");
                    try { InstanceFinder.ServerManager?.Kick(c, KickReason.Unset); } catch { }
                }
                Reply(playerControl, $"<#FF0>Banned {display} <#888>({p})");
            });
        }

        private static void HandleUnbanCommand(PlayerControl playerControl, string args)
        {
            string query = args?.Trim();
            if (string.IsNullOrWhiteSpace(query)) { Reply(playerControl, "<#F00>Usage: !unban <name|puid>"); return; }

            // A banned PUID matches directly; otherwise resolve by stored player name.
            string puid = (LooksLikePuid(query) && Perms.IsBanned(query)) ? query : Perms.FindBannedPuidByName(query);
            if (string.IsNullOrWhiteSpace(puid)) { Reply(playerControl, $"<#FA0>No ban found for: {query}"); return; }

            string display = AutoCloseTmpRichText(Perms.GetName(puid) ?? puid);
            string p = puid;
            RequestConfirmation(playerControl, $"Unban {display}", () =>
                Reply(playerControl, Perms.Unban(p) ? $"<#FF0>Unbanned {display}." : $"<#FA0>No ban found for {display}."));
        }

        private static void HandleOpCommand(PlayerControl playerControl, string args)
        {
            (string levelStr, string name) = SplitFirstWord(args);
            if (string.IsNullOrWhiteSpace(levelStr) || string.IsNullOrWhiteSpace(name) || !PermLevelExtensions.TryParse(levelStr, out PermLevel level))
            { Reply(playerControl, "<#F00>Usage: !op <everyone|mod|admin|owner> <name>"); return; }

            PlayerReference target = Utils.FindPlayerByName(name, sanitized: true);
            if (target == null || string.IsNullOrWhiteSpace(target.ProductUserId))
            { Reply(playerControl, $"<#FA0>Player not found (must be online): {name}"); return; }

            string puid = target.ProductUserId; string targetName = StripRichText(target.Username); PermLevel lvl = level;
            string display = AutoCloseTmpRichText(target.Username);
            RequestConfirmation(playerControl, $"Set {display} to {lvl.DisplayName()}", () =>
            {
                Perms.SetLevel(puid, lvl, targetName);
                Reply(playerControl, $"<#FF0>{display} is now {lvl.DisplayName()}.");
            });
        }

        private static void HandleLevelCommand(PlayerControl playerControl, string args)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Reply(playerControl, $"<#7FF>Your level: {Perms.GetLevel(playerControl.OwnerId).DisplayName()}");
                return;
            }
            PlayerReference target = Utils.FindPlayerByName(args.Trim(), sanitized: true);
            if (target == null)
            {
                Reply(playerControl, $"<#FA0>Player not found: {args.Trim()}");
                return;
            }
            Reply(playerControl, $"<#7FF>{AutoCloseTmpRichText(target.Username)}: {Perms.GetLevel(target.ConnectionID).DisplayName()}");
        }
    }
}

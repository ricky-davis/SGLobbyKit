using System;
using System.Reflection;
using HarmonyLib;
using Il2CppFishNet;
using MelonLoader;

namespace LobbyKit.Features.Fixes
{
    // Restores cannon launches on a dedicated/host server.
    //
    // A cannon is a Seat: sitting + clicking sends PlayerControl.Cmd_DoSeatAction -> (server)
    // Cannon.Server_StartSeatAction -> PlayerControl.Server_GetHitBySomething(launchForce, self, ...) which
    // TargetRpcs the launch to the player. Server_GetHitBySomething suppresses a SELF-inflicted hit while
    // PlayerSnowmanRollingController.JustRolledSnowball() is true (so finishing a snowball roll doesn't knock
    // you down). JustRolledSnowball() returns `justRolledCooldown > 0`.
    //
    // The bug: the server sets justRolledCooldown = 1.5 in Cmd_StopRolling (when a player finishes rolling a
    // snowball), but PlayerSnowmanRollingController.Update only decrements it `if (IsOwner)` — and the server is
    // never the owner of a remote player's controller — so on the server it STICKS at 1.5 forever. Once a player
    // rolls a snowball, every cannon self-launch is permanently suppressed and the cannon does nothing.
    //
    // JustRolledSnowball() is read ONLY by Server_GetHitBySomething, so forcing it false on the server fully
    // fixes cannons with no other effect (the server's 1.5s grace window is already broken/stuck anyway). Runs
    // only on the server (the only place JustRolledSnowball is consulted); real clients are untouched.
    internal static class CannonLaunchFix
    {
        public static void Apply(HarmonyLib.Harmony harmony)
        {
            try
            {
                var t = typeof(Il2Cpp.PlayerSnowmanRollingController);
                var m = t.GetMethod("JustRolledSnowball", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (m == null)
                {
                    MelonLogger.Warning("[LobbyKit] CannonLaunchFix: PlayerSnowmanRollingController.JustRolledSnowball not found; cannon fix disabled.");
                    return;
                }

                harmony.Patch(m, prefix: new HarmonyMethod(typeof(CannonLaunchFix), nameof(JustRolledSnowball_Prefix)));
                MelonLogger.Msg("[LobbyKit] CannonLaunchFix: JustRolledSnowball forced false on server — restores cannon launches (server-side justRolledCooldown never decrements).");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[LobbyKit] CannonLaunchFix patch failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // Only override on the server — that's the only side that suffers the stuck cooldown and the only place
        // JustRolledSnowball is consulted (Server_GetHitBySomething). Fail-open: any error runs the original.
        private static bool JustRolledSnowball_Prefix(ref bool __result)
        {
            try
            {
                if (!InstanceFinder.IsServer) return true; // run original on a pure client
            }
            catch { return true; }

            __result = false;
            return false; // skip original
        }
    }
}

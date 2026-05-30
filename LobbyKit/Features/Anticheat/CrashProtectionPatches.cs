using System;
using HarmonyLib;
using MelonLoader;

namespace LobbyKit.Patches
{
    [HarmonyPatch]
    internal static class CrashProtectionPatches
    {
        // Any NetworkObject whose stop callback throws (e.g. a TrinketPack with an
        // uninitialized SyncVar from a test-spawned object) will abort FishNet's despawn
        // loop, leaving the server in a corrupt state that prevents clean restart. Suppress
        // exceptions here so teardown continues past any broken NetworkObject.
        [HarmonyPatch(typeof(Il2CppFishNet.Object.NetworkObject), "InvokeStopCallbacks")]
        [HarmonyFinalizer]
        private static Exception NetworkObject_InvokeStopCallbacks_Finalizer(Exception __exception)
        {
            if (__exception != null)
                MelonLogger.Warning($"[LobbyKit] NetworkObject stop callback threw during teardown (suppressed): {__exception.GetType().Name}");
            return null;
        }
    }
}

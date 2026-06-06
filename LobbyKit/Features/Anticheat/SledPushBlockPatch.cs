using System;
using System.Reflection;
using HarmonyLib;
using Il2CppFishNet;
using MelonLoader;

namespace LobbyKit.Features.Anticheat
{
    // No-ops the CLIENT-initiated sled push (Cmd_PushSled) while leaving SpeedBoosts working.
    //
    // Cmd_PushSled(Vector3 force, bool doEffects) is a ServerRpc on Sled: the client hands the server an
    // arbitrary force that goes straight into Rigidbody.AddForce in Server_PushSled (no cap) — a raw
    // speed/launch lever. BUT Server_PushSled is ALSO the sink for legit SpeedBoosts: SpeedBoost.OnTriggerEnter
    // -> Sled.Server_SpeedBoost_Flat -> Server_PushSled. So patching Server_PushSled outright kills boosts too
    // (confirmed live). And patching only RpcLogic misses every REMOTE client (their Cmd runs
    // RpcReader___Cmd_PushSled -> Server_PushSled DIRECTLY, never touching RpcLogic — see
    // [[fishnet-serverrpc-patch-sink]]).
    //
    // So we bracket the two Cmd entry points (RpcReader for remote, RpcLogic for local/host) with a depth
    // counter and no-op Server_PushSled ONLY while we're inside one. A SpeedBoost's Server_PushSled call has
    // depth 0 -> allowed; a Cmd's has depth > 0 -> blocked. The Cmd readers still run fully (we don't skip
    // them), so no PooledReader desync. A finalizer (not a postfix) decrements, so a throw inside a reader can
    // never leave the counter stuck and silently break boosts. Fail-open everywhere: any error -> allow.
    internal static class SledPushBlockPatch
    {
        private static int _cmdPushDepth;
        private static int _blockedCount;
        private static float _lastLog;

        public static void Apply(HarmonyLib.Harmony harmony)
        {
            try
            {
                var t = typeof(Il2Cpp.Sled);
                var bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                MethodInfo sink = t.GetMethod("Server_PushSled", bf);
                if (sink == null)
                {
                    MelonLogger.Warning("[LobbyKit] Anticheat: Sled.Server_PushSled not found; sled-push block disabled.");
                    return;
                }

                MethodInfo reader = t.GetMethod("RpcReader___Cmd_PushSled___1082256137", bf);
                MethodInfo logic = t.GetMethod("RpcLogic___Cmd_PushSled___1082256137", bf);

                var enter = new HarmonyMethod(typeof(SledPushBlockPatch), nameof(EnterCmdPush));
                var leave = new HarmonyMethod(typeof(SledPushBlockPatch), nameof(LeaveCmdPush));
                if (reader != null) harmony.Patch(reader, prefix: enter, finalizer: leave);
                if (logic != null) harmony.Patch(logic, prefix: enter, finalizer: leave);

                harmony.Patch(sink, prefix: new HarmonyMethod(typeof(SledPushBlockPatch), nameof(SinkPrefix)));
                MelonLogger.Msg($"[LobbyKit] Anticheat: sled-push block installed (Cmd_PushSled blocked, SpeedBoost allowed; reader={reader != null}, logic={logic != null}).");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[LobbyKit] Anticheat: sled-push block patch failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // Bracket the Cmd entry points. EnterCmdPush returns void (never skips the reader -> no desync); the
        // finalizer always runs, even if the reader throws, so the counter can't get stuck.
        private static void EnterCmdPush() { _cmdPushDepth++; }

        private static Exception LeaveCmdPush(Exception __exception)
        {
            if (_cmdPushDepth > 0) _cmdPushDepth--;
            return __exception;   // propagate any original exception unchanged (don't swallow)
        }

        // The shared sink: block AddForce only when we got here from a client Cmd push (depth > 0).
        // SpeedBoosts (Server_SpeedBoost_Flat -> here) have depth 0 and pass through untouched.
        private static bool SinkPrefix(Il2Cpp.Sled __instance)
        {
            try
            {
                if (!InstanceFinder.IsServerStarted) return true;   // only act as the host
                if (!LobbyKitCore.BlockSledPush) return true;       // pref off -> allow all pushes
                if (_cmdPushDepth <= 0) return true;                // not a client Cmd (e.g. SpeedBoost) -> allow

                _blockedCount++;
                float now;
                try { now = UnityEngine.Time.unscaledTime; } catch { now = 0f; }
                if (now - _lastLog >= 5f)
                {
                    _lastLog = now;
                    int owner = -1;
                    try { owner = __instance.OwnerId; } catch { }
                    MelonLogger.Msg($"[LobbyKit] Anticheat: blocked Cmd_PushSled (latest sled owner={owner}; {_blockedCount} total since start).");
                }
                return false;   // no-op the client's manual push
            }
            catch
            {
                return true;    // fail-open: never break a legit push (boost) on our error
            }
        }
    }
}

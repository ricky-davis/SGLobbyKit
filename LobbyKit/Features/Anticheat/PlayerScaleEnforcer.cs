using System.Collections.Generic;
using Il2Cpp;
using Il2CppFishNet;
using MelonLoader;
using UnityEngine;

namespace LobbyKit.Features.Anticheat
{
    // Server-side anticheat: keeps every non-host player's avatar root at scale 1,1,1 on the SERVER.
    //
    // Design (LobbyKit is server-first):
    //   - Runs ONLY on the host (InstanceFinder.IsServerStarted). No-op on pure clients.
    //   - Never relies on clients running LobbyKit.
    //   - Never touches the host's own player.
    //
    // NOTE on scope: player size is a CLIENT-AUTHORITATIVE NetworkTransform value, so the owning client
    // wins on its OWN screen. The only way to override the owner's local view is to transfer ownership —
    // but that churns FishNet ownership state and corrupts this game's fragile join/spawn path (breaks
    // players joining), so we do NOT do that. Instead we simply clamp the SERVER's authoritative copy to
    // 1,1,1 (no ownership change, no network churn): this keeps the server's own copy correct and is safe
    // during joins. A cheater may still see themselves enlarged on their own screen (cosmetic). To also
    // stop the cheat for OTHER players reliably, escalate to a kick (see KickOversizedPlayers option).
    internal static class PlayerScaleEnforcer
    {
        private const float Tolerance = 0.001f;
        private const float CheckIntervalSeconds = 0.5f;

        private static float _timer;
        private static PlayerControl[] _cache;
        private static readonly Dictionary<int, float> _lastLoggedByOwner = new();

        public static void Tick()
        {
            if (!InstanceFinder.IsServerStarted)   // host-only
                return;

            _timer -= Time.unscaledDeltaTime;
            if (_cache == null || _timer <= 0f)
            {
                _cache = UnityEngine.Object.FindObjectsOfType<PlayerControl>();
                _timer = CheckIntervalSeconds;
            }

            int hostConnId = 32767;
            try { hostConnId = InstanceFinder.ClientManager.Connection.ClientId; } catch { }

            foreach (var pc in _cache)
            {
                if (pc == null)
                    continue;

                int owner;
                try { owner = pc.OwnerId; } catch { continue; }
                if (owner == hostConnId)            // never affect the host's own player
                    continue;

                Transform t = pc.transform;
                Vector3 s = t.localScale;
                if (Mathf.Abs(s.x - 1f) <= Tolerance && Mathf.Abs(s.y - 1f) <= Tolerance && Mathf.Abs(s.z - 1f) <= Tolerance)
                    continue;

                t.localScale = Vector3.one;   // clamp the server's copy only — no ownership transfer
                LogCorrection(owner, s);
            }
        }

        private static void LogCorrection(int owner, Vector3 was)
        {
            float now = Time.unscaledTime;
            if (_lastLoggedByOwner.TryGetValue(owner, out float last) && now - last < 5f)
                return;
            _lastLoggedByOwner[owner] = now;
            MelonLogger.Msg($"[LobbyKit] Anticheat: player size (client={owner}) was ({was.x:F2},{was.y:F2},{was.z:F2}) — clamped server copy to 1,1,1.");
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppFishNet;
using Il2CppFishNet.Component.Transforming;
using Il2CppFishNet.Serializing;
using MelonLoader;

namespace LobbyKit.Features.Anticheat
{
    // Server-side player-size control + anticheat. Each player's avatar scale is forced to their REGISTERED
    // size (PlayerSizeRegistry, default 1.0) inside the FishNet transform packet, so the SERVER and every OTHER
    // client render them at that size — with no ownership transfer (which churns ownership state and breaks
    // joining players). Host-only, never relies on clients.
    //
    // A scale a player did not earn (a cheat, or any value != their registered size) is rewritten back here.
    // The !size command sets a player's registered size within [0.5, 3.0] and pushes it out (see ApplySize).
    // Because the NetworkTransform is client-authoritative, the owner's OWN screen ignores server transforms
    // (MoveToTarget hard-returns for IsOwner, GameAssembly.dll.c:1153198) — so we briefly forge them non-owner
    // (the ownership nudge / ApplySize push) to make their client adopt the server value, with no broadcast and
    // no RebuildObservers, so joins are unaffected.
    //
    // FishNet's client-authoritative NetworkTransform relays the owner's RAW serialized packet to observers,
    // so we rewrite the scale bytes on the server before they're stored/forwarded.
    //
    // Wire format (SerializeChanged): [flagA][posX?][posY?][posZ?][rot?][flagB][scaleX?][scaleY?][scaleZ?][parent?]
    //   flagA bits: posX i16(0x01)/single(0x02), posY i16(0x04)/single(0x08), posZ i16(0x10)/single(0x20),
    //               rotation(0x40), extended-section-present(0x80).
    //   flagB bits (only if flagA&0x80): scaleX i16(0x01)/single(0x02), scaleY i16(0x04)/single(0x08),
    //               scaleZ i16(0x10)/single(0x20), parent(0x40), teleport(0x80).
    //   Each scale axis is Int16(scale*100) [2B] or full Single [4B] — both fixed width, so we overwrite the
    //   value bytes in place with 1.0 (Int16 100 / Single 1.0f). Scale is only present when it CHANGES, so
    //   normal movement packets early-out on byte 0.
    //
    // Failsafe: the parsed structure must consume EXACTLY the whole packet (length self-check). Any mismatch,
    // any parenting, any exception -> the packet is left 100% untouched (worst case: cheat not clamped, never
    // a corrupt transform packet).
    internal static class PlayerScalePacketClamp
    {
        // _packing.Rotation == Packed (AutoPackType.Packed=1) → Writer.WriteQuaternion uses Quaternion32
        // compression = 4 bytes (NOT 8: that's PackedLess/Quaternion64). Rotation is only in the packet when
        // the player is turning (walking), so an off-by-this failed the p==n self-check and silently skipped
        // every rotating packet. The self-check still guards this value.
        private const int QuaternionBytes = 4;
        private const float Tolerance = 0.01f;
        private static readonly Dictionary<int, float> _lastLoggedByOwner = new();

        public static void Apply(HarmonyLib.Harmony harmony)
        {
            try
            {
                MethodInfo m = typeof(NetworkTransform).GetMethod(
                    "RpcLogic___ServerUpdateTransform___2713644489",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (m == null)
                {
                    MelonLogger.Warning("[LobbyKit] Anticheat: ServerUpdateTransform not found; scale packet clamp disabled.");
                    return;
                }
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(PlayerScalePacketClamp), nameof(Prefix)));
                MelonLogger.Msg("[LobbyKit] Anticheat: player-scale packet clamp installed.");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[LobbyKit] Anticheat: scale packet clamp patch failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void Prefix(NetworkTransform __instance, Il2CppSystem.ArraySegment<byte> __0)
        {
            try
            {
                if (!LobbyKitCore.BlockPlayerSizeCheat) return;
                if (!InstanceFinder.IsServerStarted) return;

                var arr = __0.Array;
                if (arr == null) return;
                int o = __0.Offset;
                int n = __0.Count;
                if (n < 2) return;

                byte flagA = arr[o];
                if ((flagA & 0x80) == 0) return;   // no extended section -> no scale (the common, hot path)

                int hostConnId = 32767;
                try { hostConnId = InstanceFinder.ClientManager.Connection.ClientId; } catch { }
                int owner;
                try { owner = __instance.OwnerId; } catch { return; }
                if (owner == hostConnId) return;                            // never touch the host's own player
                if (__instance.GetComponent<PlayerControl>() == null) return;  // players only

                float target = PlayerSizeRegistry.GetSize(owner);            // their !size choice, default 1.0

                int p = 1;
                p += (flagA & 0x01) != 0 ? 2 : 0; p += (flagA & 0x02) != 0 ? 4 : 0;   // posX
                p += (flagA & 0x04) != 0 ? 2 : 0; p += (flagA & 0x08) != 0 ? 4 : 0;   // posY
                p += (flagA & 0x10) != 0 ? 2 : 0; p += (flagA & 0x20) != 0 ? 4 : 0;   // posZ
                if ((flagA & 0x40) != 0) p += QuaternionBytes;                        // rotation
                if (p >= n) return;

                byte flagB = arr[o + p];
                p += 1;
                if ((flagB & 0x40) != 0) return;   // parent present (variable length) -> skip, very rare

                int scaleStart = o + p;
                int sx = (flagB & 0x01) != 0 ? 2 : ((flagB & 0x02) != 0 ? 4 : 0);
                int sy = (flagB & 0x04) != 0 ? 2 : ((flagB & 0x08) != 0 ? 4 : 0);
                int sz = (flagB & 0x10) != 0 ? 2 : ((flagB & 0x20) != 0 ? 4 : 0);
                p += sx + sy + sz;

                if (p != n) return;                // structure must consume the whole packet, else fail safe
                if (sx == 0 && sy == 0 && sz == 0) return;

                // Only act if the scale deviates from the player's registered size (leave matching packets alone).
                int ri = scaleStart;
                float vx = ReadOneScale(arr, ref ri, sx);
                float vy = ReadOneScale(arr, ref ri, sy);
                float vz = ReadOneScale(arr, ref ri, sz);
                if (NearTarget(vx, target) && NearTarget(vy, target) && NearTarget(vz, target)) return;

                int wi = scaleStart;
                wi = WriteOneScale(arr, wi, sx, target);
                wi = WriteOneScale(arr, wi, sy, target);
                wi = WriteOneScale(arr, wi, sz, target);

                LogClamp(owner, vx, vy, vz, target);

                // Also correct the player's OWN screen: briefly tell only their client it doesn't own the
                // object so it applies the server's clamped scale, then tell it it owns the object again.
                TriggerOwnershipNudge(__instance, owner);
            }
            catch
            {
                // Never let a parse error corrupt the packet or throw into FishNet's RPC path.
            }
        }

        // ── Ownership nudge: fixes the cheater's own view without a real ownership change ───────────────
        // A client-authoritative owner ignores all server transforms for its own object (MoveToTarget hard-
        // returns for IsOwner). We forge an ownership-change packet (id 11) sent ONLY to the cheater telling
        // them they no longer own the object — their NetworkTransform then applies the server's authoritative
        // (clamped) scale=1 to their own view — then forge it back. The server's real ownership never changes,
        // so there is NO RebuildObservers and NO broadcast to anyone else: joins are completely unaffected.
        // Validated live. For a one-time size cheat this corrects the owner's view permanently; a continuously
        // re-applied cheat would re-snap on the next cooldown.
        private const short OwnershipChangePacketId = 11;
        // Owners with a nudge currently in flight. We don't time-throttle: every cheat packet re-nudges as soon
        // as the previous nudge finishes, so re-applied cheats are corrected promptly (a continuously re-applied
        // cheat just gets nudged back-to-back ~every NudgeHoldSeconds — a constant hitch for the cheater).
        private static readonly HashSet<int> _nudgingOwners = new();

        private static void TriggerOwnershipNudge(NetworkTransform nt, int ownerId)
        {
            try
            {
                if (_nudgingOwners.Contains(ownerId)) return;   // a nudge for this owner is already running
                var nob = nt.NetworkObject;
                var conn = nt.Owner;
                if (nob == null || conn == null || !conn.IsValid) return;
                _nudgingOwners.Add(ownerId);
                MelonCoroutines.Start(NudgeCoroutine(nob, conn, ownerId));
            }
            catch { }
        }

        private static IEnumerator NudgeCoroutine(Il2CppFishNet.Object.NetworkObject nob, Il2CppFishNet.Connection.NetworkConnection conn, int ownerId)
        {
            try
            {
                SendOwnershipChange(nob, Il2CppFishNet.Managing.NetworkManager.EmptyConnection, conn); // "you don't own this"

                // Wait exactly one FishNet tick before restoring, so the restore goes out in a SEPARATE send
                // batch than the remove (otherwise they coalesce and the client never spends a tick as a
                // non-owner -> MoveToTarget never applies scale=1). Tick-synchronized = tightest reliable gap.
                var tm = InstanceFinder.TimeManager;
                if (tm != null)
                {
                    var startTick = tm.Tick;
                    int guard = 0;
                    while (tm.Tick == startTick && guard++ < 120)   // guard caps the wait if ticks ever stall
                        yield return null;
                }
                else
                {
                    yield return new UnityEngine.WaitForSecondsRealtime(0.05f);
                }

                if (nob != null && conn != null && conn.IsValid)
                    SendOwnershipChange(nob, conn, conn);                                              // "you own this again"
            }
            finally
            {
                _nudgingOwners.Remove(ownerId);   // ready to re-nudge on the next cheat packet
            }
        }

        // Sends a forged FishNet ownership-change packet ([int16 id=11][NetworkObject][NetworkConnection])
        // to a single target client only (mirrors GiveOwnership's per-connection send; never broadcasts).
        private static void SendOwnershipChange(Il2CppFishNet.Object.NetworkObject nob,
                                                Il2CppFishNet.Connection.NetworkConnection reportedOwner,
                                                Il2CppFishNet.Connection.NetworkConnection target)
        {
            try
            {
                var nm = InstanceFinder.NetworkManager;
                if (nm == null || nob == null || target == null) return;
                var w = WriterPool.Retrieve(nm);
                w.WriteInt16(OwnershipChangePacketId);
                w.WriteNetworkObject(nob);
                w.WriteNetworkConnection(reportedOwner);
                var seg = w.GetArraySegment();
                nm.TransportManager.SendToClient((byte)0, seg, target, true, default(Il2CppFishNet.Object.DataOrderType));
                w.Store();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[LobbyKit] Anticheat: ownership nudge send failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static bool NearTarget(float v, float target) => Math.Abs(v - target) <= Tolerance;

        private static float ReadOneScale(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<byte> arr, ref int idx, int len)
        {
            if (len == 2) { short s = (short)(arr[idx] | (arr[idx + 1] << 8)); idx += 2; return s / 100f; }
            if (len == 4)
            {
                int bits = arr[idx] | (arr[idx + 1] << 8) | (arr[idx + 2] << 16) | (arr[idx + 3] << 24);
                idx += 4; return BitConverter.Int32BitsToSingle(bits);
            }
            return 1f;
        }

        // Overwrites a scale axis value with `target` in its existing encoding (little-endian):
        //   2 bytes -> Int16(target * 100)   |   4 bytes -> Single(target)
        private static int WriteOneScale(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<byte> arr, int idx, int len, float target)
        {
            if (len == 2)
            {
                short s = (short)UnityEngine.Mathf.RoundToInt(target * 100f);
                arr[idx] = (byte)(s & 0xFF);
                arr[idx + 1] = (byte)((s >> 8) & 0xFF);
                return idx + 2;
            }
            if (len == 4)
            {
                byte[] b = BitConverter.GetBytes(target);   // little-endian on x86/x64
                arr[idx] = b[0]; arr[idx + 1] = b[1]; arr[idx + 2] = b[2]; arr[idx + 3] = b[3];
                return idx + 4;
            }
            return idx;
        }

        private static void LogClamp(int owner, float vx, float vy, float vz, float target)
        {
            float now;
            try { now = UnityEngine.Time.unscaledTime; } catch { now = 0f; }
            if (_lastLoggedByOwner.TryGetValue(owner, out float last) && now - last < 5f) return;
            _lastLoggedByOwner[owner] = now;
            MelonLogger.Msg($"[LobbyKit] Anticheat: corrected player (client={owner}) scale ({vx:F2},{vy:F2},{vz:F2}) -> {target:0.##} for the server and all other clients.");
        }

        // ── !size apply: push a player's chosen size out to everyone ─────────────────────────────────────
        // Called by the !size command. Sets the server's authoritative copy, relays the new scale to all
        // OTHER clients, and ownership-forges the OWNER (whose client-auth NetworkTransform otherwise ignores
        // server transforms) so their own screen updates too. The pushed packet carries the player's CURRENT
        // position (single-encoded) so applying it only changes scale — it never moves them. After this, the
        // clamp's per-user target keeps them there (and snaps cheats back).
        public static void ApplySize(PlayerControl pc, float size)
        {
            try
            {
                if (pc == null) return;

                pc.transform.localScale = new UnityEngine.Vector3(size, size, size);   // server copy + new joiners

                var nob = pc.NetworkObject;
                var nt = pc.GetComponent<NetworkTransform>();
                if (nob == null || nt == null) return;
                var conn = nob.Owner;

                // Everyone except the owner: relay the transform (the owner's reader skips this RPC for itself).
                PushSizeToObservers(pc, size);

                // The owner: forge them non-owner so MoveToTarget applies it, deliver the goal, restore ownership.
                if (conn != null && conn.IsValid)
                {
                    byte[] packet = BuildPositionScalePacket(pc.transform.position, size);
                    MelonCoroutines.Start(PushSizeToOwner(nob, nt, conn, packet));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[LobbyKit] !size apply failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // Relays a player's size to all CURRENT observers (the owner skips this RPC for itself). Late joiners
        // spawn the player from the server's cached transform and never received the original ObserversUpdate,
        // so RepushAllSizes() must be re-run when someone joins (see LobbyKitCore.PlayerJoinedGame).
        public static void PushSizeToObservers(PlayerControl pc, float size)
        {
            try
            {
                if (pc == null) return;
                var nt = pc.GetComponent<NetworkTransform>();
                if (nt == null) return;
                byte[] packet = BuildPositionScalePacket(pc.transform.position, size);
                nt.RpcWriter___ObserversUpdateClientAuthoritativeTransform___2713644489(ToSeg(packet), Il2CppFishNet.Transporting.Channel.Reliable);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[LobbyKit] !size observer relay failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // Re-broadcasts every non-default registered size to all observers. Called (delayed + retried) when a
        // player joins, so late joiners pick up sizes that were applied before they arrived. Re-applying to
        // players who already have the size is harmless. No-op when nobody has a custom size.
        public static void RepushAllSizes()
        {
            try
            {
                if (!InstanceFinder.IsServerStarted || !PlayerSizeRegistry.AnySizes) return;
                foreach (var pc in UnityEngine.Object.FindObjectsOfType<PlayerControl>())
                {
                    int owner;
                    try { owner = pc.OwnerId; } catch { continue; }
                    float size = PlayerSizeRegistry.GetSize(owner);
                    if (Math.Abs(size - PlayerSizeRegistry.DefaultSize) <= Tolerance) continue;
                    PushSizeToObservers(pc, size);
                }
            }
            catch { }
        }

        private static IEnumerator PushSizeToOwner(Il2CppFishNet.Object.NetworkObject nob, NetworkTransform nt,
                                                   Il2CppFishNet.Connection.NetworkConnection conn, byte[] packet)
        {
            SendOwnershipChange(nob, Il2CppFishNet.Managing.NetworkManager.EmptyConnection, conn);   // "you don't own this"
            yield return WaitOneTick();                                                              // separate send batch
            try { nt.RpcWriter___TargetUpdateTransform___748863190(conn, ToSeg(packet), Il2CppFishNet.Transporting.Channel.Reliable); } catch { }
            yield return new UnityEngine.WaitForSecondsRealtime(0.4f);                               // let the scale reach target
            if (nob != null && conn != null && conn.IsValid)
                SendOwnershipChange(nob, conn, conn);                                                // "you own this again"
        }

        // Full transform packet carrying position (single) + scale (single). Position is included so the client
        // applies the goal without moving — only scale changes. flagA: posX|posY|posZ single + extended(0x80);
        // flagB: scaleX|scaleY|scaleZ single. 1 + 12 + 1 + 12 = 26 bytes.
        private static byte[] BuildPositionScalePacket(UnityEngine.Vector3 pos, float scale)
        {
            byte[] pkt = new byte[26];
            pkt[0] = 0xAA;                       // posX(0x02) posY(0x08) posZ(0x20) extended(0x80)
            WriteFloatLE(pkt, 1, pos.x);
            WriteFloatLE(pkt, 5, pos.y);
            WriteFloatLE(pkt, 9, pos.z);
            pkt[13] = 0x2A;                      // scaleX(0x02) scaleY(0x08) scaleZ(0x20)
            WriteFloatLE(pkt, 14, scale);
            WriteFloatLE(pkt, 18, scale);
            WriteFloatLE(pkt, 22, scale);
            return pkt;
        }

        private static void WriteFloatLE(byte[] arr, int idx, float f)
        {
            byte[] b = BitConverter.GetBytes(f);
            arr[idx] = b[0]; arr[idx + 1] = b[1]; arr[idx + 2] = b[2]; arr[idx + 3] = b[3];
        }

        private static Il2CppSystem.ArraySegment<byte> ToSeg(byte[] data)
        {
            var arr = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>(data.Length);
            for (int i = 0; i < data.Length; i++) arr[i] = data[i];
            return new Il2CppSystem.ArraySegment<byte>(arr);
        }

        private static IEnumerator WaitOneTick()
        {
            var tm = InstanceFinder.TimeManager;
            if (tm != null)
            {
                var startTick = tm.Tick;
                int guard = 0;
                while (tm.Tick == startTick && guard++ < 120)
                    yield return null;
            }
            else
            {
                yield return new UnityEngine.WaitForSecondsRealtime(0.05f);
            }
        }
    }
}

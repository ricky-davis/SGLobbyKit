using System.Collections.Generic;

namespace LobbyKit.Features.Anticheat
{
    // Per-player avatar size chosen via the !size command. Keyed on connectionId, which matches both the
    // NetworkTransform OwnerId (used by the scale clamp) and PlayerReference.ConnectionID (used by the chat
    // command + leave hook). Default is 1.0 (normal). Entries are removed when a player leaves
    // (LobbyKitCore.PlayerLeftGame) and cleared on session reset, so a reconnecting player is back at normal
    // until they !size again.
    //
    // The scale clamp reads GetSize() as its enforced target: any scale packet a player sends is rewritten to
    // their registered size (default 1.0), so this doubles as the size anticheat — players can only be a size
    // the host's range allows, and cheats are clamped back to it.
    internal static class PlayerSizeRegistry
    {
        public const float MinSize = 0.2f;
        public const float MaxSize = 3.0f;
        public const float DefaultSize = 1.0f;

        private static readonly Dictionary<int, float> _sizes = new();

        public static float GetSize(int connectionId)
            => _sizes.TryGetValue(connectionId, out float s) ? s : DefaultSize;

        public static bool HasSize(int connectionId) => _sizes.ContainsKey(connectionId);

        // Stores the size if it is within [MinSize, MaxSize]. Returns false (and stores nothing) if out of range.
        public static bool TrySetSize(int connectionId, float size)
        {
            if (size < MinSize || size > MaxSize)
                return false;
            _sizes[connectionId] = size;
            return true;
        }

        public static void Remove(int connectionId) => _sizes.Remove(connectionId);

        public static void Clear() => _sizes.Clear();
    }
}

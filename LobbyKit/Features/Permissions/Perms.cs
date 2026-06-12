using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LobbyKit.Features.Permissions
{
    // JSON-backed permission store, persisted to UserData/LobbyKit-permissions.json. Holds op levels, banned
    // players, and per-command required levels. Keyed on EOS ProductUserId (PUID) so a player's level/ban
    // survives reconnects. Each player/ban entry also stores an optional last-known name (refreshed on join)
    // purely for human reference and name-based lookups. The host (connection 32767) is always Owner.
    internal static class Perms
    {
        private const int HostConnectionId = 32767;

        // A player op entry. Accepts either a bare integer (level only) or an object { level, name } in JSON.
        [JsonConverter(typeof(PlayerEntryConverter))]
        private sealed class PlayerEntry { public int level; public string name; }

        // A ban entry. Accepts either a bare string (reason only) or an object { reason, name } in JSON.
        [JsonConverter(typeof(BanEntryConverter))]
        private sealed class BanEntry { public string reason; public string name; }

        private sealed class PermData
        {
            public string[] _readme { get; set; }
            public Dictionary<string, PlayerEntry> players { get; set; } = new();
            public Dictionary<string, int> commandLevels { get; set; } = new();
            public Dictionary<string, BanEntry> banned { get; set; } = new();
        }

        private static readonly string[] ReadmeLines =
        {
            "LobbyKit permissions. Edit while the server is STOPPED, then restart (no hot-reload).",
            "Levels: Everyone=0, Mod=1, Admin=2, Owner=3. The host (the server itself) is always Owner.",
            "players: \"<PUID>\": { \"level\": 2, \"name\": \"...\" }.  You may also write just the level: \"<PUID>\": 2.",
            "  PUIDs appear in the join log: + Name joined (conn X, puid <PUID>). Names auto-fill on join.",
            "commandLevels: \"<command>\": required level.  Set a level above 3 (e.g. 4) to disable a command.",
            "banned: \"<PUID>\": { \"reason\": \"...\", \"name\": \"...\" }.  Manage with !ban / !unban in chat.",
            "Example - make someone an Admin:  \"players\": { \"00024765...ee5c\": { \"level\": 2 } }"
        };

        private static PermData _data = new();
        private static string FilePath => Path.Combine(MelonEnvironment.UserDataDirectory, "LobbyKit-permissions.json");

        public static void Initialize()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    _data = JsonConvert.DeserializeObject<PermData>(File.ReadAllText(FilePath)) ?? new PermData();
                    _data.players ??= new();
                    _data.commandLevels ??= new();
                    _data.banned ??= new();
                    MelonLogger.Msg($"[LobbyKit] Permissions: {_data.players.Count} op(s), {_data.banned.Count} ban(s), {_data.commandLevels.Count} command level(s).");
                }
                else
                {
                    Save();
                    MelonLogger.Msg($"[LobbyKit] Permissions file created: {FilePath}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[LobbyKit] Permissions load failed: {ex.GetType().Name}: {ex.Message}. Using empty set.");
                _data = new PermData();
            }
        }

        public static void Save()
        {
            try
            {
                _data._readme = ReadmeLines;   // refresh the help header every save so it's never lost
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(_data, Formatting.Indented));
            }
            catch (Exception ex) { MelonLogger.Warning($"[LobbyKit] Permissions save failed: {ex.GetType().Name}: {ex.Message}"); }
        }

        // ── Levels ───────────────────────────────────────────────────────────────────
        public static PermLevel GetLevel(string puid)
            => string.IsNullOrWhiteSpace(puid) ? PermLevel.Everyone
             : (_data.players.TryGetValue(puid, out PlayerEntry e) ? Clamp(e.level) : PermLevel.Everyone);

        public static PermLevel GetLevel(int connectionId)
            => connectionId == HostConnectionId ? PermLevel.Owner : GetLevel(ResolvePuid(connectionId));

        public static bool Has(int connectionId, PermLevel min) => GetLevel(connectionId) >= min;
        public static bool Has(Il2Cpp.PlayerControl pc, PermLevel min) => pc != null && Has(pc.OwnerId, min);

        public static void SetLevel(string puid, PermLevel level, string name = null)
        {
            if (string.IsNullOrWhiteSpace(puid)) return;
            if (level <= PermLevel.Everyone) { _data.players.Remove(puid); }
            else
            {
                if (!_data.players.TryGetValue(puid, out PlayerEntry e)) { e = new PlayerEntry(); _data.players[puid] = e; }
                e.level = (int)level;
                if (!string.IsNullOrWhiteSpace(name)) e.name = name;
            }
            Save();
        }

        // ── Names ────────────────────────────────────────────────────────────────────
        public static string GetName(string puid)
        {
            if (string.IsNullOrWhiteSpace(puid)) return null;
            if (_data.players.TryGetValue(puid, out PlayerEntry p) && !string.IsNullOrWhiteSpace(p.name)) return p.name;
            if (_data.banned.TryGetValue(puid, out BanEntry b) && !string.IsNullOrWhiteSpace(b.name)) return b.name;
            return null;
        }

        // Refreshes the stored name for a PUID on any op/ban entry that exists (we only keep names for tracked
        // players). Saves only when something actually changed, to avoid churning the file on every join.
        public static void RememberName(string puid, string name)
        {
            if (string.IsNullOrWhiteSpace(puid) || string.IsNullOrWhiteSpace(name)) return;
            bool changed = false;
            if (_data.players.TryGetValue(puid, out PlayerEntry p) && p.name != name) { p.name = name; changed = true; }
            if (_data.banned.TryGetValue(puid, out BanEntry b) && b.name != name) { b.name = name; changed = true; }
            if (changed) Save();
        }

        // ── Bans ─────────────────────────────────────────────────────────────────────
        public static bool IsBanned(string puid) => !string.IsNullOrWhiteSpace(puid) && _data.banned.ContainsKey(puid);
        public static string BanReason(string puid) => _data.banned.TryGetValue(puid ?? string.Empty, out BanEntry b) ? b.reason : string.Empty;

        public static void Ban(string puid, string reason, string name = null)
        {
            if (string.IsNullOrWhiteSpace(puid)) return;
            _data.banned[puid] = new BanEntry { reason = string.IsNullOrWhiteSpace(reason) ? "Banned" : reason, name = name };
            _data.players.Remove(puid);   // a banned player loses any op level
            Save();
        }

        public static bool Unban(string puid)
        {
            if (string.IsNullOrWhiteSpace(puid) || !_data.banned.Remove(puid)) return false;
            Save();
            return true;
        }

        // Finds a banned PUID by stored name (case-insensitive). Returns null if no banned player matches.
        public static string FindBannedPuidByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            foreach (KeyValuePair<string, BanEntry> kv in _data.banned)
                if (string.Equals(kv.Value.name, name, StringComparison.OrdinalIgnoreCase))
                    return kv.Key;
            return null;
        }

        // ── Per-command required level (JSON-configurable) ───────────────────────────
        public static int GetCommandLevel(string name, int defaultLevel)
            => _data.commandLevels.TryGetValue(Normalize(name), out int lvl) ? lvl : defaultLevel;

        public static void SeedCommandLevels(IEnumerable<KeyValuePair<string, int>> defaults)
        {
            bool changed = false;
            foreach (KeyValuePair<string, int> kv in defaults)
            {
                string key = Normalize(kv.Key);
                if (!_data.commandLevels.ContainsKey(key)) { _data.commandLevels[key] = kv.Value; changed = true; }
            }
            if (changed) Save();
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────
        private static string ResolvePuid(int connectionId)
        {
            try { return Utils.FindPlayerFromConnectionId(connectionId)?.ProductUserId; }
            catch { return null; }
        }

        private static string Normalize(string command) => (command ?? string.Empty).Trim().ToLowerInvariant().TrimStart('!');
        private static PermLevel Clamp(int lvl) => (PermLevel)Math.Max(0, Math.Min(3, lvl));

        // Reads "<puid>": 2  OR  "<puid>": { "level": 2, "name": "..." }; writes the object form.
        private sealed class PlayerEntryConverter : JsonConverter<PlayerEntry>
        {
            public override PlayerEntry ReadJson(JsonReader reader, Type t, PlayerEntry existing, bool has, JsonSerializer s)
            {
                if (reader.TokenType == JsonToken.Integer) return new PlayerEntry { level = Convert.ToInt32(reader.Value) };
                JObject o = JObject.Load(reader);
                return new PlayerEntry { level = o.Value<int?>("level") ?? 0, name = (string)o["name"] };
            }
            public override void WriteJson(JsonWriter writer, PlayerEntry v, JsonSerializer s)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("level"); writer.WriteValue(v.level);
                if (!string.IsNullOrEmpty(v.name)) { writer.WritePropertyName("name"); writer.WriteValue(v.name); }
                writer.WriteEndObject();
            }
        }

        // Reads "<puid>": "reason"  OR  "<puid>": { "reason": "...", "name": "..." }; writes the object form.
        private sealed class BanEntryConverter : JsonConverter<BanEntry>
        {
            public override BanEntry ReadJson(JsonReader reader, Type t, BanEntry existing, bool has, JsonSerializer s)
            {
                if (reader.TokenType == JsonToken.String) return new BanEntry { reason = (string)reader.Value };
                JObject o = JObject.Load(reader);
                return new BanEntry { reason = (string)o["reason"], name = (string)o["name"] };
            }
            public override void WriteJson(JsonWriter writer, BanEntry v, JsonSerializer s)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("reason"); writer.WriteValue(v.reason ?? "Banned");
                if (!string.IsNullOrEmpty(v.name)) { writer.WritePropertyName("name"); writer.WriteValue(v.name); }
                writer.WriteEndObject();
            }
        }
    }
}

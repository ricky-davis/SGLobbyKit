using System;
using System.Collections.Generic;
using Il2Cpp;
using LobbyKit.Features.Permissions;
using LobbyKit.Features.Settings;

namespace LobbyKit.Patches
{
    // Chat-driven settings: !settings, !settings <page>, !settings <key>, !settings <key> <value>.
    // Runs server-side, so a non-host Admin/Mod (per permissions.json on "!settings") can edit the host's
    // settings from chat without opening the local UI menu. A local host with no args still opens the UI menu.
    public static partial class ChatSystem
    {
        private sealed class ChatSetting
        {
            public string Key;
            public string Section;
            public Func<string> Show;                          // current value, display form
            public Func<string, (bool ok, string msg)> Apply;  // parse + set, returns a plain result message
        }

        private static ChatSetting SBool(string key, string section, Func<bool> get, Action<bool> set) => new ChatSetting
        {
            Key = key,
            Section = section,
            Show = () => get() ? "on" : "off",
            Apply = v =>
            {
                bool? b = ParseOnOff(v);
                if (b == null) return (false, $"{key}: use on/off");
                set(b.Value);
                return (true, $"{key} = {(b.Value ? "on" : "off")}");
            }
        };

        private static ChatSetting SInt(string key, string section, Func<int> get, Action<int> set, int min, int max) => new ChatSetting
        {
            Key = key,
            Section = section,
            Show = () => get().ToString(),
            Apply = v =>
            {
                if (!int.TryParse(v.Trim(), out int n)) return (false, $"{key}: number {min}-{max}");
                n = Math.Max(min, Math.Min(max, n));
                set(n);
                return (true, $"{key} = {n}");
            }
        };

        // mask: never show the value (e.g. password). strip: show without rich-text tags (e.g. prefixes).
        private static ChatSetting SText(string key, string section, Func<string> get, Action<string> set, bool mask = false, bool strip = false) => new ChatSetting
        {
            Key = key,
            Section = section,
            Show = () =>
            {
                string s = get();
                if (string.IsNullOrEmpty(s)) return "(empty)";
                if (mask) return "(set)";
                return strip ? StripRichText(s) : s;
            },
            Apply = v =>
            {
                string val = v.Trim();
                if (val.Equals("clear", StringComparison.OrdinalIgnoreCase) || val == "-") val = string.Empty;
                set(val);
                return (true, val.Length == 0 ? $"{key} cleared" : $"{key} set");
            }
        };

        private static bool? ParseOnOff(string v) => (v ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "on" or "true" or "1" or "yes" or "enable" or "enabled" => true,
            "off" or "false" or "0" or "no" or "disable" or "disabled" => false,
            _ => (bool?)null
        };

        // The full editable-setting registry, grouped by section (order preserved for the list view).
        private static readonly List<ChatSetting> SettingsRegistry = new()
        {
            SBool("bangcommands",          "Mod",   () => LobbyKitCore.EnableGuestBangCommands, LobbyKitCore.SetEnableGuestBangCommands),
            SBool("autorestart", "Mod",   () => LobbyKitCore.AutoRestartOnCrash,    LobbyKitCore.SetAutoRestartOnCrash),

            SText("motd",        "Messages", () => LobbyKitCore.MessageOfTheDay,    LobbyKitCore.SetMessageOfTheDay),
            SBool("jm",          "Messages", () => LobbyKitCore.ShowJoinMessages,   LobbyKitCore.SetShowJoinMessages),
            SInt ("jmsize",      "Messages", () => LobbyKitCore.JoinMessageSize,    LobbyKitCore.SetJoinMessageSize, 50, 100),
            SBool("lm",          "Messages", () => LobbyKitCore.ShowLeaveMessages,  LobbyKitCore.SetShowLeaveMessages),
            SInt ("lmsize",      "Messages", () => LobbyKitCore.LeaveMessageSize,   LobbyKitCore.SetLeaveMessageSize, 50, 100),
            SBool("kickmsg",     "Messages", () => LobbyKitCore.ShowKickBanMessages, LobbyKitCore.SetShowKickBanMessages),

            SBool("throwspam",   "Anticheat", () => LobbyKitCore.BlockThrowingSpam,    LobbyKitCore.SetBlockThrowingSpam),
            SBool("sizecheat",   "Anticheat", () => LobbyKitCore.BlockPlayerSizeCheat, LobbyKitCore.SetBlockPlayerSizeCheat),
            SBool("flyingsleds", "Anticheat", () => LobbyKitCore.BlockFlyingSleds,     LobbyKitCore.SetBlockFlyingSleds),

            SText("prefixmod",   "Prefixes", () => LobbyKitCore.ChatPrefixFor(PermLevel.Mod),   v => LobbyKitCore.SetChatPrefix(PermLevel.Mod, v),   strip: true),
            SText("prefixadmin", "Prefixes", () => LobbyKitCore.ChatPrefixFor(PermLevel.Admin), v => LobbyKitCore.SetChatPrefix(PermLevel.Admin, v), strip: true),
            SText("prefixowner", "Prefixes", () => LobbyKitCore.ChatPrefixFor(PermLevel.Owner), v => LobbyKitCore.SetChatPrefix(PermLevel.Owner, v), strip: true),
        };

        private static readonly Dictionary<string, ChatSetting> SettingsByKey = BuildSettingsByKey();

        private static Dictionary<string, ChatSetting> BuildSettingsByKey()
        {
            var d = new Dictionary<string, ChatSetting>(StringComparer.OrdinalIgnoreCase);
            foreach (ChatSetting s in SettingsRegistry) d[s.Key] = s;
            return d;
        }

        private static void HandleSettingsCommand(PlayerControl playerControl, string args)
        {
            string a = args?.Trim() ?? string.Empty;

            // "!settings ?" always shows the chat list, even for the local host (who otherwise gets the UI menu).
            if (a == "?")
            {
                ShowSettingsList(playerControl, 1);
                return;
            }

            // No args: a local host (with a real UI) opens the menu; non-host admins (and headless) get the list.
            if (a.Length == 0)
            {
                if (IsLocalConnectionId(playerControl.OwnerId))
                    OpenSettingsMenu(playerControl, args);
                else
                    ShowSettingsList(playerControl, 1);
                return;
            }

            // !settings <page>
            if (int.TryParse(a, out int pageArg))
            {
                ShowSettingsList(playerControl, pageArg);
                return;
            }

            // !settings <key> [value]
            (string key, string value) = SplitFirstWord(a);
            if (!SettingsByKey.TryGetValue(key, out ChatSetting setting))
            {
                Reply(playerControl, $"<#FA0>Unknown setting: {key} (try `!settings ?`)");
                return;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                Reply(playerControl, $"<#7FF>{setting.Key} = {setting.Show()}");
                return;
            }

            (bool ok, string msg) = setting.Apply(value);
            Reply(playerControl, (ok ? "<#FF0>" : "<#FA0>") + msg);
        }

        // Paged chat listing of all settings + current values, grouped by section (uses the help line budget).
        private static void ShowSettingsList(PlayerControl playerControl, int page)
        {
            var lines = new List<(string text, bool divider)>();
            string lastSection = null;
            foreach (ChatSetting s in SettingsRegistry)
            {
                if (s.Section != lastSection)
                {
                    lines.Add(($"-- {s.Section} --", true));
                    lastSection = s.Section;
                }
                lines.Add((HelpTruncate($"{s.Key} = {s.Show()}", HelpLineChars), false));
            }

            int perPage = HelpLinesPerPage - 1; // reserve the header line
            int pages = Math.Max(1, (lines.Count + perPage - 1) / perPage);
            page = Math.Max(1, Math.Min(page, pages));

            string header = $"Settings (Page {page}/{pages})";
            if (page < pages) header += $" !settings {page + 1}";
            Reply(playerControl, HelpLine(HelpTruncate(header, HelpLineChars), header: true));

            int start = (page - 1) * perPage;
            int stop = Math.Min(start + perPage, lines.Count);
            for (int i = start; i < stop; i++)
                Reply(playerControl, HelpLine(lines[i].text, header: lines[i].divider));
        }
    }
}

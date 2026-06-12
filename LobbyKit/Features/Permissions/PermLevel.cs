using System;

namespace LobbyKit.Features.Permissions
{
    // Linear permission ranks. A command declares a MinLevel floor (the minimum to invoke it at all); handlers
    // self-gate elevated branches (e.g. setting the MOTD vs reading it) with Perms.Has(player, PermLevel.X).
    public enum PermLevel
    {
        Everyone = 0,
        Mod = 1,
        Admin = 2,
        Owner = 3
    }

    public static class PermLevelExtensions
    {
        public static string DisplayName(this PermLevel level) => level switch
        {
            PermLevel.Owner => "Owner",
            PermLevel.Admin => "Admin",
            PermLevel.Mod => "Mod",
            _ => "Everyone"
        };

        // Parses a level from a name or number (case-insensitive). "everyone"/"none"/"remove"/"0" -> Everyone.
        public static bool TryParse(string input, out PermLevel level)
        {
            level = PermLevel.Everyone;
            switch ((input ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "owner": case "3": level = PermLevel.Owner; return true;
                case "admin": case "2": level = PermLevel.Admin; return true;
                case "mod": case "moderator": case "1": level = PermLevel.Mod; return true;
                case "everyone": case "none": case "guest": case "remove": case "deop": case "0":
                    level = PermLevel.Everyone; return true;
                default: return false;
            }
        }
    }
}

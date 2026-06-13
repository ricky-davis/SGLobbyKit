using Il2Cpp;
using Il2CppFishNet.Connection;
using UnityEngine;
using Object = UnityEngine.Object;
using System.Text.RegularExpressions;
using Il2CppDissonance.Integrations.FishNet;
using DissonancePlayer = Il2CppDissonance.Integrations.FishNet.DissonanceFishNetPlayer;
using NetworkObject = Il2CppFishNet.Object.NetworkObject;
using UnityObject = UnityEngine.Object;

namespace LobbyKit
{
    internal static class Utils
    {
        public static PlayerControl FindHostPlayer()
        {
            var players = UnityEngine.Object.FindObjectsOfType<PlayerControl>();

            foreach (var pc in players)
            {
                if (pc == null)
                    continue;

                try
                {
                    if (pc.IsOwner)
                        return pc;
                }
                catch
                {
                    continue;
                }
            }

            return null;
        }

        public static PlayerReference FindPlayerByName(string name, float similarityThreshold = 0.1f, bool sanitized = false)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            var manager = PlayerReferenceManager.Instance;
            if (manager == null || manager.sync_PlayerReferences == null)
                return null;

            // Collect valid (name, reference) pairs, then let NameMatch do the scoring/selection so the mod
            // and the tools/NameFuzzTest harness resolve names through the exact same code.
            var refs = new List<PlayerReference>();
            var names = new List<string>();
            for (int i = 0; i < manager.sync_PlayerReferences.Count; i++)
            {
                PlayerReference playerRef = manager.sync_PlayerReferences[i];
                if (playerRef == null)
                    continue;

                string username = sanitized ? NameMatch.Sanitize(playerRef.Username) : playerRef.Username;
                if (string.IsNullOrWhiteSpace(username))
                    continue;

                refs.Add(playerRef);
                names.Add(username);
            }

            int idx = NameMatch.BestMatchIndex(names, name, similarityThreshold);
            return idx >= 0 ? refs[idx] : null;
        }

        // EXACT (case-insensitive, sanitized) name match only — returns null if no connected player's name
        // equals `name`. Unlike FindPlayerByName this never fuzzy-matches, so it's safe for detecting the
        // exact boundary of a player name inside a longer argument string (e.g. "<name> <reason>"), including
        // names with spaces, quotes, or other special characters.
        public static PlayerReference FindPlayerByExactName(string name, bool sanitized = true)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            var manager = PlayerReferenceManager.Instance;
            if (manager == null || manager.sync_PlayerReferences == null)
                return null;

            for (int i = 0; i < manager.sync_PlayerReferences.Count; i++)
            {
                PlayerReference playerRef = manager.sync_PlayerReferences[i];
                if (playerRef == null)
                    continue;

                string username = sanitized ? SanitizeUsername(playerRef.Username) : playerRef.Username;
                if (string.IsNullOrWhiteSpace(username))
                    continue;

                if (username.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return playerRef;
            }

            return null;
        }

        public static PlayerReference FindPlayerFromConnectionId(int connectionId)
        {
            var manager = PlayerReferenceManager.Instance;
            if (manager == null || manager.sync_PlayerReferences == null)
                return null;

            for (int i = 0; i < manager.sync_PlayerReferences.Count; i++)
            {
                PlayerReference playerRef = manager.sync_PlayerReferences[i];
                if (playerRef == null)
                    continue;

                if (playerRef.ConnectionID == connectionId)
                    return playerRef;
            }

            return null;
        }

        public static GameObject FindChildByName(Transform parent, string name)
        {
            if (parent == null)
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);

                if (child.name == name)
                    return child.gameObject;

                GameObject found = FindChildByName(child, name);
                if (found != null)
                    return found;
            }

            return null;
        }
        public static DissonancePlayer GetDissonancePlayer(PlayerControl pc)
        {
            if (pc == null)
                return null;

            NetworkObject pcNo = pc.GetComponent<NetworkObject>();
            if (pcNo == null)
                return null;

            int ownerId = pcNo.OwnerId;

            foreach (DissonancePlayer d in UnityObject.FindObjectsOfType<DissonancePlayer>())
            {
                NetworkObject dNo = d.GetComponent<NetworkObject>();

                if (dNo != null && dNo.OwnerId == ownerId)
                    return d;
            }

            return null;
        }

        // Rich-text colour-tag stripping lives in NameMatch (the dependency-free matcher) so the standalone
        // test harness shares it; kept here as a thin alias for existing callers.
        public static string SanitizeUsername(string name) => NameMatch.Sanitize(name);
    }
}

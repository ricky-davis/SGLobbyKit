using Il2Cpp;
using Il2CppFishNet.Connection;
using Object = UnityEngine.Object;

namespace MultiplayerTools
{
    internal static class Utils
    {
        public static PlayerControl FindHostPlayer()
        {
            var players = Object.FindObjectsOfType<PlayerControl>();
            foreach (var pc in players)
            {
                if (pc != null && pc.IsOwner)
                    return pc;
            }

            return Object.FindObjectOfType<PlayerControl>();
        }

        public static PlayerReference FindPlayerByName(string name, float similarityThreshold = 0.1f)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            var manager = PlayerReferenceManager.Instance;
            if (manager == null || manager.sync_PlayerReferences == null)
                return null;

            PlayerReference bestMatch = null;
            float bestScore = 0f;

            for (int i = 0; i < manager.sync_PlayerReferences.Count; i++)
            {
                PlayerReference playerRef = manager.sync_PlayerReferences[i];
                string username = playerRef.Username;
                if (string.IsNullOrWhiteSpace(username))
                    continue;

                if (username.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return playerRef;

                float score = CalculateSimilarity(username, name);
                if (score > bestScore && score >= similarityThreshold)
                {
                    bestScore = score;
                    bestMatch = playerRef;
                }
            }

            return bestMatch;
        }

        public static PlayerControl FindPlayerFromConnectionId(int connectionId)
        {
            var manager = PlayerReferenceManager.Instance;
            if (manager == null || manager.sync_PlayerReferences == null)
                return null;

            for (int i = 0; i < manager.sync_PlayerReferences.Count; i++)
            {
                PlayerReference playerRef = manager.sync_PlayerReferences[i];
                if (playerRef.ConnectionID == connectionId)
                    return playerRef.PlayerControl;
            }

            return null;
        }

        private static float CalculateSimilarity(string a, string b)
        {
            int distance = LevenshteinDistance(a.ToLowerInvariant(), b.ToLowerInvariant());
            int maxLen = Math.Max(a.Length, b.Length);
            if (maxLen == 0) return 1f;
            return 1f - (float)distance / maxLen;
        }

        private static int LevenshteinDistance(string s, string t)
        {
            int n = s.Length, m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            if (n == 0) return m;
            if (m == 0) return n;

            for (int i = 0; i <= n; d[i, 0] = i++) { }
            for (int j = 0; j <= m; d[0, j] = j++) { }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[n, m];
        }
    }
}

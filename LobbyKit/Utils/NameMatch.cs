using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace LobbyKit
{
    // Pure (no Il2Cpp / Unity dependencies) fuzzy player-name matching — the single source of truth for how
    // LobbyKit resolves a typed name to a player. Utils.FindPlayerByName feeds connected usernames through
    // this; the standalone harness in tools/NameFuzzTest links THIS FILE directly (no copy) so tests always
    // run the real logic.
    public static class NameMatch
    {
        public const float DefaultThreshold = 0.1f;

        private static readonly Regex HexColorTag =
            new Regex(@"<#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})>", RegexOptions.Compiled);

        // Strips TMP/rich-text <#hex> colour tags so they don't pollute matching/display.
        public static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            return HexColorTag.Replace(name, "");
        }

        // Picks the best candidate index for a query, or -1 if nothing qualifies. Mirrors FindPlayerByName's
        // selection: an exact (case-insensitive) name wins outright; otherwise the highest Similarity that
        // meets the threshold wins, ties resolved by list order (first wins). `names` should already be
        // sanitized the same way the caller wants them compared.
        public static int BestMatchIndex(IReadOnlyList<string> names, string query, float threshold = DefaultThreshold)
        {
            if (names == null || string.IsNullOrWhiteSpace(query)) return -1;

            int best = -1;
            float bestScore = 0f;
            for (int i = 0; i < names.Count; i++)
            {
                string candidate = names[i];
                if (string.IsNullOrWhiteSpace(candidate)) continue;

                if (candidate.Equals(query, StringComparison.OrdinalIgnoreCase))
                    return i;   // exact match short-circuits, same as FindPlayerByName

                float score = Similarity(candidate, query);
                if (score > bestScore && score >= threshold)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        // Name-match score in [0,1]. `candidate` is a player's (display) name, `query` is what was typed.
        //
        // Tiered and CONTAINMENT-AWARE: a name that contains the query (as a whole word, a prefix, or a
        // substring) always outscores one that is merely edit-distance-similar. A plain whole-string
        // Levenshtein (the old approach) has no notion of containment — so a short query like "Snail" scored
        // worse against "Mr. Snail Dev (Snev)" (lots of chars to insert) than against a shorter name sharing
        // a prefix ("SnakeyJakey"), and "dev" matched "dan" over "Mr. Snail Dev (Snev)". The bands below are
        // ordered so fuzzy fallback can never beat a containment match; within a tier, the match closest in
        // length to the query wins.
        public static float Similarity(string candidate, string query)
        {
            string name = (candidate ?? string.Empty).ToLowerInvariant().Trim();
            string q = (query ?? string.Empty).ToLowerInvariant().Trim();
            if (q.Length == 0 || name.Length == 0) return 0f;
            if (name == q) return 1f;

            List<string> tokens = Tokenize(name);   // "mr. snail dev (snev)" -> [mr, snail, dev, snev]

            // 1) A whole word of the name equals the query.            e.g. "dev"->Dev, "snev"->(Snev)
            foreach (string t in tokens)
                if (t == q) return 0.97f;

            // 2) A word of the name starts with the query.             e.g. "sna"->snail, "snake"->snakeyjakey
            float tokenPrefix = 0f;
            foreach (string t in tokens)
                if (t.StartsWith(q, StringComparison.Ordinal))
                    tokenPrefix = Math.Max(tokenPrefix, 0.90f + 0.05f * ((float)q.Length / t.Length));
            if (tokenPrefix > 0f) return tokenPrefix;

            // 3) The full name starts with the query.
            if (name.StartsWith(q, StringComparison.Ordinal))
                return 0.85f + 0.04f * ((float)q.Length / name.Length);

            // 4) A word of the name contains the query.
            float tokenContains = 0f;
            foreach (string t in tokens)
                if (t.Contains(q))
                    tokenContains = Math.Max(tokenContains, 0.75f + 0.05f * ((float)q.Length / t.Length));
            if (tokenContains > 0f) return tokenContains;

            // 5) The full name contains the query anywhere.
            if (name.Contains(q))
                return 0.65f + 0.05f * ((float)q.Length / name.Length);

            // 6) Fuzzy fallback (typos): best edit-distance similarity vs the whole name or any single word,
            //    capped at 0.5 so a non-containing name can never outrank a containment match (tiers 1-5).
            float fuzzy = LevenshteinSimilarity(name, q);
            foreach (string t in tokens)
                fuzzy = Math.Max(fuzzy, LevenshteinSimilarity(t, q));
            return fuzzy * 0.5f;
        }

        // Splits a name into lowercase alphanumeric words, dropping spaces/punctuation. So "Mr. Snail Dev
        // (Snev)" -> ["mr", "snail", "dev", "snev"], letting "dev" / "snev" match by whole word.
        private static List<string> Tokenize(string s)
        {
            var list = new List<string>();
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (sb.Length > 0) { list.Add(sb.ToString()); sb.Clear(); }
            }
            if (sb.Length > 0) list.Add(sb.ToString());
            return list;
        }

        private static float LevenshteinSimilarity(string a, string b)
        {
            int max = Math.Max(a.Length, b.Length);
            if (max == 0) return 1f;
            return 1f - (float)LevenshteinDistance(a, b) / max;
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

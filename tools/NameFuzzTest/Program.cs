// Standalone test harness for LobbyKit's fuzzy player-name matching.
//
// It does NOT copy any logic — the .csproj links the mod's real LobbyKit/NameMatch.cs, so the scoring,
// sanitizing, and selection here are the exact same code that `!tp`/`!tpf`/`!kick`/... use in-game. Only the
// roster below is local data.
//
// Run:
//   dotnet run                       # interactive: type a query per line (Ctrl-D / Ctrl-Z to quit)
//   dotnet run -- Snail snev dev      # one-shot: test the given queries and exit
//   dotnet run -- --raw Snail         # match against RAW usernames (sanitized:false) instead of sanitized

using LobbyKit;

// ── Live roster (PlayerReference.Username, raw) pulled from the lobby on 2026-06-13 ──
string[] roster =
{
    "Mr. Snail Dev (Snev)",
    "mrhappy5548",
    "Autistic Osama",
    "KINO!!",
    "Uno",
    "AlxGee",
    "SnakeyJakey",
    "evan",
    "Spyci",
    "Gurnal Sanders",
    "Shibby",
    "Shlewp",
    "Tevin_Lucky",
    "xenielle",
    "F4ble",
    "AddaBerry42",
    "jameson21314",
    "isand.69",
    "BusyBee",
    "KrispyKrap",
    "CraftyCrumbs",
    "𝕊𝕡𝕣𝕦𝕟𝕜𝕚 ඞ𝕟𝕚𝕞𝕥𝕠𝕟𝕤",
    "SerpenSorpia",
    "CvrKing",
    "NightShade",
    "begichan",
    "Aldeeezyx",
    "FloralValentine",
    "<color=#9933FF>King_Kretzy</color>",
    "Frankie",
};

bool useRaw = false;
var queries = new List<string>();
foreach (var a in args)
{
    if (a == "--raw") useRaw = true;
    else queries.Add(a);
}

// Candidate display names, sanitized the same way FindPlayerByName(sanitized: true) does (unless --raw).
string[] candidates = roster.Select(r => useRaw ? r : NameMatch.Sanitize(r)).ToArray();

if (queries.Count > 0)
{
    foreach (var q in queries) Report(q);
}
else
{
    Console.WriteLine($"Roster: {roster.Length} names. Sanitized matching = {!useRaw} (pass --raw to match raw names).");
    Console.WriteLine("Type a query and press Enter. Ctrl-D (Unix) / Ctrl-Z (Windows) to quit.\n");
    Console.Write("> ");
    string line;
    while ((line = Console.ReadLine()) != null)
    {
        if (line.Length > 0) Report(line);
        Console.Write("> ");
    }
}

void Report(string query)
{
    // Winner via the real selection logic; ranked list via the real scorer.
    int idx = NameMatch.BestMatchIndex(candidates, query);

    Console.WriteLine($"\nquery: \"{query}\"");
    if (idx >= 0)
    {
        bool exact = candidates[idx].Equals(query, StringComparison.OrdinalIgnoreCase);
        float winScore = exact ? 1f : NameMatch.Similarity(candidates[idx], query);
        Console.WriteLine($"  => MATCH: \"{roster[idx]}\"  (score {winScore:0.000}{(exact ? ", exact" : "")})");
    }
    else
    {
        Console.WriteLine($"  => NO MATCH (nothing scored >= {NameMatch.DefaultThreshold:0.00})");
    }

    Console.WriteLine("  top candidates:");
    var ranked = Enumerable.Range(0, roster.Length)
        .Select(i => (i, score: NameMatch.Similarity(candidates[i], query)))
        .OrderByDescending(x => x.score)
        .Take(6);
    foreach (var (i, score) in ranked)
    {
        string mark = i == idx ? " <=" : "";
        string shown = candidates[i] == roster[i] ? $"\"{roster[i]}\"" : $"\"{candidates[i]}\" (raw \"{roster[i]}\")";
        Console.WriteLine($"    {score:0.000}  {shown}{mark}");
    }
}

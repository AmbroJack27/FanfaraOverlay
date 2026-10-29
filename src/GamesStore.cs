using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Fanfara;

/// <summary>Per-game achievement completion, used to find games finished 100%.</summary>
public class GameRecord
{
    public uint Appid { get; set; }
    public string Name { get; set; } = "";
    public int Total { get; set; }        // total achievements the game has
    public int Unlocked { get; set; }     // how many the user has unlocked
    public long LastUnlock { get; set; }  // unix seconds of the most recent unlock (completion date for 100%)

    public bool IsPerfect => Total > 0 && Unlocked >= Total;
}

/// <summary>
/// Local, per-user store of per-game completion (feeds the "100% games" cards and the leaderboard
/// score). Lives in %AppData%\Fanfara\games.json and never leaves the PC. Populated by the Steam
/// backfill (which knows each game's total vs unlocked) and nudged by live unlocks.
/// </summary>
public class GamesStore
{
    private readonly string _path;
    private readonly object _gate = new();
    private readonly Dictionary<uint, GameRecord> _byApp = new();

    public GamesStore()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Fanfara");
        try { Directory.CreateDirectory(dir); } catch { }
        _path = Path.Combine(dir, "games.json");
        Load();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var list = JsonSerializer.Deserialize<List<GameRecord>>(File.ReadAllText(_path));
            if (list == null) return;
            lock (_gate)
                foreach (var g in list)
                    if (g.Appid != 0) _byApp[g.Appid] = g;
        }
        catch { /* start empty on any problem */ }
    }

    /// <summary>Record a game's total/unlocked counts from a sync. Keeps the latest known completion.</summary>
    public void Set(uint appid, string name, int total, int unlocked, long lastUnlock)
    {
        if (appid == 0) return;
        lock (_gate)
        {
            if (!_byApp.TryGetValue(appid, out var g)) { g = new GameRecord { Appid = appid }; _byApp[appid] = g; }
            if (!string.IsNullOrEmpty(name)) g.Name = name;
            if (total > 0) g.Total = total;
            g.Unlocked = unlocked;
            if (lastUnlock > g.LastUnlock) g.LastUnlock = lastUnlock;
        }
    }

    /// <summary>A live unlock: bump the unlocked count for a game (total may be unknown until the next sync).</summary>
    public void BumpUnlocked(uint appid, string name, long when)
    {
        if (appid == 0) return;
        lock (_gate)
        {
            if (!_byApp.TryGetValue(appid, out var g)) { g = new GameRecord { Appid = appid }; _byApp[appid] = g; }
            if (!string.IsNullOrEmpty(name) && string.IsNullOrEmpty(g.Name)) g.Name = name;
            g.Unlocked++;
            if (g.Total > 0 && g.Unlocked > g.Total) g.Unlocked = g.Total;
            if (when > g.LastUnlock) g.LastUnlock = when;
        }
    }

    /// <summary>Games completed 100%, most recently completed first.</summary>
    public List<GameRecord> PerfectGames()
    {
        lock (_gate)
        {
            var l = new List<GameRecord>();
            foreach (var g in _byApp.Values) if (g.IsPerfect) l.Add(g);
            l.Sort((a, b) => b.LastUnlock.CompareTo(a.LastUnlock));
            return l;
        }
    }

    /// <summary>Number of games completed 100% (the leaderboard score).</summary>
    public int PerfectCount
    {
        get { lock (_gate) { int n = 0; foreach (var g in _byApp.Values) if (g.IsPerfect) n++; return n; } }
    }

    /// <summary>Total unlocked achievements across all games (the leaderboard tie-breaker).</summary>
    public int TotalUnlocked
    {
        get { lock (_gate) { int n = 0; foreach (var g in _byApp.Values) n += g.Unlocked; return n; } }
    }

    public void Save()
    {
        try
        {
            List<GameRecord> snapshot;
            lock (_gate) snapshot = new List<GameRecord>(_byApp.Values);
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(snapshot));
            if (File.Exists(_path)) File.Replace(tmp, _path, null); else File.Move(tmp, _path);
        }
        catch { /* best effort */ }
    }
}

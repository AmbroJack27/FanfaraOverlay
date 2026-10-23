using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Fanfara;

/// <summary>One unlocked achievement, as shown in the History and Collection windows.</summary>
public class AchievementRecord
{
    public uint Appid { get; set; }
    public string Game { get; set; } = "";
    public string Api { get; set; } = "";     // Steam's internal achievement id (dedupe key with Appid)
    public string Name { get; set; } = "";
    public string Desc { get; set; } = "";
    public long Unlock { get; set; }           // unix seconds; 0 if unknown
    public double? Percent { get; set; }       // global unlock % (rarity); null if unknown
    public string Icon { get; set; } = "";     // Steam's unlocked-achievement icon URL; "" if unknown
}

/// <summary>
/// Local, per-user store of unlocked achievements (feeds History + Collection).
/// Lives in %AppData%\Fanfara\history.json and never leaves the PC.
/// Both the live unlock path and the Steam backfill write here; dedupe is by (appid, api).
/// </summary>
public class AchievementStore
{
    private readonly string _path;
    private readonly object _gate = new();
    private readonly Dictionary<string, AchievementRecord> _byKey = new();

    public AchievementStore()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Fanfara");
        try { Directory.CreateDirectory(dir); } catch { }
        _path = Path.Combine(dir, "history.json");
        Load();
    }

    private static string Key(uint appid, string api) => appid + "\u0000" + api;

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var list = JsonSerializer.Deserialize<List<AchievementRecord>>(File.ReadAllText(_path));
            if (list == null) return;
            lock (_gate)
                foreach (var r in list)
                    if (!string.IsNullOrEmpty(r.Api)) _byKey[Key(r.Appid, r.Api)] = r;
        }
        catch { /* start empty on any problem */ }
    }

    /// <summary>Add or update a record. Returns true if this unlock is new (first time we record it).</summary>
    public bool Upsert(AchievementRecord r)
    {
        if (r == null || string.IsNullOrEmpty(r.Api)) return false;
        lock (_gate)
        {
            var k = Key(r.Appid, r.Api);
            bool isNew = !_byKey.ContainsKey(k);
            if (!isNew)
            {
                var old = _byKey[k];
                // Keep the earliest known unlock time, and don't lose an icon/percent a prior pass had.
                if (r.Unlock == 0 && old.Unlock != 0) r.Unlock = old.Unlock;
                if (string.IsNullOrEmpty(r.Icon) && !string.IsNullOrEmpty(old.Icon)) r.Icon = old.Icon;
                if (r.Percent == null && old.Percent != null) r.Percent = old.Percent;
            }
            _byKey[k] = r;
            return isNew;
        }
    }

    /// <summary>All records, most recent unlock first.</summary>
    public List<AchievementRecord> All()
    {
        lock (_gate)
        {
            var l = new List<AchievementRecord>(_byKey.Values);
            l.Sort((a, b) => b.Unlock.CompareTo(a.Unlock));
            return l;
        }
    }

    public int Count { get { lock (_gate) return _byKey.Count; } }

    public void Save()
    {
        try
        {
            List<AchievementRecord> snapshot;
            lock (_gate) snapshot = new List<AchievementRecord>(_byKey.Values);
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(snapshot));
            // Atomic-ish replace so a crash mid-write can't corrupt the store.
            if (File.Exists(_path)) File.Replace(tmp, _path, null); else File.Move(tmp, _path);
        }
        catch { /* best effort */ }
    }
}

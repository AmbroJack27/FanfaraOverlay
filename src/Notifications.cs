using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Fanfara;

/// <summary>One entry of the bell in the Home.</summary>
public class NotificationItem
{
    public string Id { get; set; } = "";
    public long Ts { get; set; }                 // unix seconds
    public string Kind { get; set; } = "";       // friend100 | mine100 | update | updated | styles
    public string Who { get; set; } = "";        // friend's Steam name
    public string Avatar { get; set; } = "";     // friend's avatar URL
    public string Game { get; set; } = "";
    public uint Appid { get; set; }
    public int N { get; set; }                   // friend100: their games at 100% · mine100: my Nth game at 100%
    public string Ver { get; set; } = "";        // update / updated
    public bool Read { get; set; }
}

/// <summary>
/// The bell's list (0.6.0): friends who completed a game, my own 100%, new versions, news.
/// Lives in %AppData%\Fanfara\notifications.json and never leaves the PC.
/// </summary>
public static class Notifications
{
    private const int Max = 80;
    private static readonly object Gate = new();
    private static readonly string PathFile = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Fanfara", "notifications.json");
    private static List<NotificationItem>? _items;

    /// <summary>Raised (on any thread) when the list changes, so the Home can refresh its bell.</summary>
    public static event Action? Changed;

    private static List<NotificationItem> Items()
    {
        if (_items != null) return _items;
        try
        {
            if (File.Exists(PathFile))
                _items = JsonSerializer.Deserialize<List<NotificationItem>>(File.ReadAllText(PathFile));
        }
        catch { }
        return _items ??= new List<NotificationItem>();
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PathFile)!);
            var tmp = PathFile + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Items()));
            if (File.Exists(PathFile)) File.Replace(tmp, PathFile, null); else File.Move(tmp, PathFile);
        }
        catch { }
    }

    /// <summary>Add an entry. Update/news entries are kept once per version (or once ever, for "styles").</summary>
    public static NotificationItem? Add(string kind, string who = "", string avatar = "", string game = "", uint appid = 0, int n = 0, string ver = "")
    {
        NotificationItem it;
        lock (Gate)
        {
            var l = Items();
            if ((kind == "update" || kind == "updated") && l.Any(x => x.Kind == kind && x.Ver == ver)) return null;
            if (kind == "styles" && l.Any(x => x.Kind == "styles")) return null;
            if ((kind == "friend100" || kind == "mine100") && appid != 0 &&
                l.Any(x => x.Kind == kind && x.Appid == appid && x.Who == who)) return null;
            it = new NotificationItem
            {
                Id = Guid.NewGuid().ToString("N")[..10], Ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Kind = kind, Who = who, Avatar = avatar, Game = game, Appid = appid, N = n, Ver = ver
            };
            l.Insert(0, it);
            if (l.Count > Max) l.RemoveRange(Max, l.Count - Max);
            Save();
        }
        try { Changed?.Invoke(); } catch { }
        return it;
    }

    /// <summary>A newer version came out after an "update" entry: the older offers are no longer useful.</summary>
    public static void DropUpdatesExcept(string ver)
    {
        lock (Gate) { Items().RemoveAll(x => x.Kind == "update" && x.Ver != ver); Save(); }
    }

    public static List<NotificationItem> All() { lock (Gate) return Items().ToList(); }

    public static int Unread() { lock (Gate) return Items().Count(x => !x.Read); }

    public static void MarkAllRead()
    {
        lock (Gate) { foreach (var x in Items()) x.Read = true; Save(); }
        try { Changed?.Invoke(); } catch { }
    }
}

/// <summary>What the Home needs to know about updates (shared between the app and the Home data).</summary>
public static class AppNews
{
    /// <summary>A newer version ready to install ("" = none).</summary>
    public static string PendingUpdate { get; set; } = "";
}

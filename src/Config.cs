using System;
using System.IO;
using System.Text.Json;

namespace Fanfara;

/// <summary>User preferences, loaded from config.json next to the .exe.</summary>
public class Config
{
    public string Style { get; set; } = "arcade";     // arcade | neon | loot | terminale | scudo | ...
    public string Sound { get; set; } = "classic";    // classic | epico | levelup | ...
    // Where the notification appears on screen:
    // bottom-center | top-center | center | top-right | bottom-right | top-left | bottom-left
    public string Position { get; set; } = "bottom-center";
    public int DurationMs { get; set; } = 4200;
    public int PollMs { get; set; } = 1000;            // how often to check Steam for new unlocks
    public bool StartWithWindows { get; set; } = false; // launch automatically when Windows starts
    public string Theme { get; set; } = "dark";        // settings window appearance: "dark" | "light"
    public bool CheckUpdates { get; set; } = true;      // check GitHub for a newer version at startup

    private static string Path_ =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "config.json");

    public static Config Load()
    {
        try
        {
            if (File.Exists(Path_))
                return JsonSerializer.Deserialize<Config>(File.ReadAllText(Path_)) ?? new Config();
        }
        catch { /* fall back to defaults */ }
        return new Config();
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(Path_,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* ignore */ }
    }
}

using System;
using System.IO;
using System.Text.Json;

namespace Fanfara;

/// <summary>User preferences, loaded from config.json next to the .exe.</summary>
public class Config
{
    public string Style { get; set; } = "arcade";     // arcade | neon | loot | terminale | scudo | ...
    public string Sound { get; set; } = "classic";    // classic | epico | levelup | ...
    public int DurationMs { get; set; } = 4200;
    public int PollMs { get; set; } = 1000;            // how often to check Steam for new unlocks

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

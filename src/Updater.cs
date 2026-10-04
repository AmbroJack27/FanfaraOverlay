using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Fanfara;

/// <summary>
/// Checks GitHub Releases for a newer version and, on the user's confirmation,
/// downloads the installer (FanfaraSetup.exe) and launches it.
/// </summary>
public static class Updater
{
    private const string Owner = "AmbroJack27";
    private const string Repo = "FanfaraOverlay";
    private const string AssetName = "FanfaraSetup.exe";

    public record UpdateInfo(string Version, string Tag, string DownloadUrl);

    /// <summary>Current app version as Major.Minor.Build (revision ignored).</summary>
    public static Version CurrentVersion
    {
        get
        {
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
            return new Version(v.Major, v.Minor, v.Build < 0 ? 0 : v.Build);
        }
    }

    /// <summary>Returns info about a newer release, or null (no update / offline / error).</summary>
    public static async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Fanfara-Updater");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            var url = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
            var json = await http.GetStringAsync(url);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = root.GetProperty("tag_name").GetString() ?? "";
            var verStr = tag.TrimStart('v', 'V');
            if (!Version.TryParse(Normalize(verStr), out var latest)) return null;
            if (latest <= CurrentVersion) return null;

            string? dl = null;
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var a in assets.EnumerateArray())
                {
                    if (string.Equals(a.GetProperty("name").GetString(), AssetName, StringComparison.OrdinalIgnoreCase))
                    {
                        dl = a.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }
            }
            if (string.IsNullOrEmpty(dl)) return null;

            return new UpdateInfo(verStr, tag, dl!);
        }
        catch { return null; }
    }

    /// <summary>Downloads the installer to a temp file and starts it. Returns true on success.</summary>
    public static async Task<bool> DownloadAndRunAsync(UpdateInfo info)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Fanfara-Updater");

            var bytes = await http.GetByteArrayAsync(info.DownloadUrl);
            var path = Path.Combine(Path.GetTempPath(), $"FanfaraSetup-{info.Version}.exe");
            await File.WriteAllBytesAsync(path, bytes);

            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            return true;
        }
        catch { return false; }
    }

    // Make "0.1.4" / "0.1" parse to a 3-part Version.
    private static string Normalize(string v)
    {
        var parts = v.Split('.');
        return parts.Length switch { 1 => v + ".0.0", 2 => v + ".0", _ => v };
    }
}

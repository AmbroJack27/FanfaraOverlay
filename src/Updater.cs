using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
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
    // Optional checksum asset published next to the installer (see release workflow). When present
    // we verify the download against it before running it; when absent we fall back to running the
    // HTTPS-downloaded installer as before (older releases have no checksum).
    private const string HashAssetName = "FanfaraSetup.exe.sha256";

    public record UpdateInfo(string Version, string Tag, string DownloadUrl, string? Sha256Url);

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
            string? hashUrl = null;
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var a in assets.EnumerateArray())
                {
                    var n = a.GetProperty("name").GetString();
                    if (string.Equals(n, AssetName, StringComparison.OrdinalIgnoreCase))
                        dl = a.GetProperty("browser_download_url").GetString();
                    else if (string.Equals(n, HashAssetName, StringComparison.OrdinalIgnoreCase))
                        hashUrl = a.GetProperty("browser_download_url").GetString();
                }
            }
            if (string.IsNullOrEmpty(dl)) return null;

            return new UpdateInfo(verStr, tag, dl!, hashUrl);
        }
        catch { return null; }
    }

    /// <summary>
    /// Downloads the installer to a temp file, verifies its SHA-256 against the release's
    /// checksum asset (when one is published), and starts it. Returns true on success.
    /// If the checksum is present and does NOT match, the installer is deleted and NOT run.
    /// </summary>
    public static async Task<bool> DownloadAndRunAsync(UpdateInfo info)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Fanfara-Updater");

            var bytes = await http.GetByteArrayAsync(info.DownloadUrl);

            // Integrity check: if the release ships a .sha256 asset, the download must match it.
            if (!string.IsNullOrEmpty(info.Sha256Url))
            {
                string? expected = null;
                try { expected = ParseHash(await http.GetStringAsync(info.Sha256Url)); } catch { }
                if (!string.IsNullOrEmpty(expected))
                {
                    var actual = Convert.ToHexString(SHA256.HashData(bytes));
                    if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                        return false;   // tampered / corrupted download — refuse to run it
                }
            }

            var path = Path.Combine(Path.GetTempPath(), $"FanfaraSetup-{info.Version}.exe");
            await File.WriteAllBytesAsync(path, bytes);

            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            return true;
        }
        catch { return false; }
    }

    // Pulls the 64-hex-char SHA-256 out of a checksum file. Accepts a bare hash or the
    // "sha256sum" format ("&lt;hash&gt;  FanfaraSetup.exe").
    private static string? ParseHash(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var token = text.Trim().Split(new[] { ' ', '\t', '\r', '\n', '*' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var t in token)
            if (t.Length == 64 && IsHex(t)) return t;
        return null;
    }

    private static bool IsHex(string s)
    {
        foreach (var c in s)
            if (!Uri.IsHexDigit(c)) return false;
        return true;
    }

    // Make "0.1.4" / "0.1" parse to a 3-part Version.
    private static string Normalize(string v)
    {
        var parts = v.Split('.');
        return parts.Length switch { 1 => v + ".0.0", 2 => v + ".0", _ => v };
    }
}

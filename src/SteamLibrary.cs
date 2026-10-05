using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Fanfara;

internal static class LibLog
{
    private static readonly string Path_ =
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "fanfara-log.txt");
    public static void Write(string m)
    {
        try { File.AppendAllText(Path_, $"{DateTime.Now:HH:mm:ss}  [prescan] {m}{Environment.NewLine}"); } catch { }
    }
}

/// <summary>
/// Finds the installed Steam games and pre-applies the overlay fix (disable fullscreen
/// optimizations) to each game's main .exe, so Fanfara's notifications show over them from
/// the very first launch — no restart needed. Runs once per game folder (cached in Config),
/// on a background thread at startup.
/// </summary>
public static class SteamLibrary
{
    private const long MinExeSize = 1_500_000;   // skip small helper/installer exes
    private const int  MaxExesPerGame = 8;
    private const int  MaxDepth = 4;             // game exes live near the root (bin, Win64, ...)

    /// <summary>Pre-optimizes every not-yet-scanned installed game. Returns how many exes were newly fixed.</summary>
    public static int PreoptimizeAll(Config config)
    {
        int applied = 0, games = 0;
        bool changed = false;
        try
        {
            var libs = GetLibraryPaths();
            LibLog.Write($"avvio — {libs.Count} libreria/e Steam");
            foreach (var lib in libs)
            {
                var common = Path.Combine(lib, "steamapps", "common");
                if (!Directory.Exists(common)) continue;

                string[] gameDirs;
                try { gameDirs = Directory.GetDirectories(common); }
                catch { continue; }

                foreach (var gameDir in gameDirs)
                {
                    if (ContainsCI(config.ScannedDirs, gameDir)) continue;   // already done

                    var exes = new List<string>();
                    Walk(gameDir, 0, exes);
                    if (exes.Count > 0)
                    {
                        games++;
                        LibLog.Write($"preparato {Path.GetFileName(gameDir)}: {string.Join(", ", exes.ConvertAll(Path.GetFileName))}");
                    }
                    foreach (var exe in exes)
                    {
                        if (FsOpt.Apply(exe)) applied++;
                        if (!ContainsCI(config.OptimizedGames, exe)) config.OptimizedGames.Add(exe);
                    }
                    config.ScannedDirs.Add(gameDir);
                    changed = true;
                }
            }
            if (changed) config.Save();
            LibLog.Write($"fine — {games} giochi nuovi, {applied} exe preparati");
        }
        catch (Exception ex) { LibLog.Write($"errore: {ex.Message}"); }
        return applied;
    }

    private static List<string> GetLibraryPaths()
    {
        var result = new List<string>();
        string? steam = null;
        try { using var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"); steam = k?.GetValue("SteamPath") as string; }
        catch { }
        if (string.IsNullOrEmpty(steam)) return result;

        steam = steam.Replace('/', '\\');
        result.Add(steam);

        try
        {
            var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
            {
                var txt = File.ReadAllText(vdf);
                foreach (Match m in Regex.Matches(txt, "\"path\"\\s+\"([^\"]+)\""))
                {
                    var p = m.Groups[1].Value.Replace("\\\\", "\\");
                    if (!ContainsCI(result, p)) result.Add(p);
                }
            }
        }
        catch { }
        return result;
    }

    private static void Walk(string dir, int depth, List<string> exes)
    {
        if (depth > MaxDepth || exes.Count >= MaxExesPerGame) return;

        string[] files;
        try { files = Directory.GetFiles(dir, "*.exe"); } catch { files = Array.Empty<string>(); }
        foreach (var f in files)
        {
            if (IsGameExe(f)) exes.Add(f);
            if (exes.Count >= MaxExesPerGame) return;
        }

        string[] subs;
        try { subs = Directory.GetDirectories(dir); } catch { return; }
        foreach (var s in subs)
        {
            var name = Path.GetFileName(s).ToLowerInvariant();
            if (name.Contains("redist") || name == "directx" || name == "_commonredist") continue;
            Walk(s, depth + 1, exes);
            if (exes.Count >= MaxExesPerGame) return;
        }
    }

    private static bool IsGameExe(string path)
    {
        try
        {
            var name = Path.GetFileName(path).ToLowerInvariant();
            foreach (var bad in new[] { "unins", "vcredist", "dxsetup", "setup", "crashhandler",
                "crashreport", "dotnet", "redist", "prereq", "anticheat_setup", "touchup",
                "helper", "launcher_installer" })
                if (name.Contains(bad)) return false;

            return new FileInfo(path).Length >= MinExeSize;
        }
        catch { return false; }
    }

    private static bool ContainsCI(List<string> list, string val)
    {
        foreach (var s in list)
            if (string.Equals(s, val, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}

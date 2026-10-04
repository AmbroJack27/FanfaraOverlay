using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using Steamworks;   // Facepunch.Steamworks

namespace Fanfara;

/// <summary>
/// Runs in a SEPARATE child process ("--steam-worker &lt;appid&gt;").
/// Initializes Steam as the running game, watches for freshly unlocked achievements, and
/// prints one line per unlock to stdout:  UNLOCK\t{name}\t{globalPercent}
///
/// IMPORTANT: it detects that the game has closed by looking at the real game PROCESS
/// (anything running from the game's install folder), NOT at Steam's RunningAppID — because
/// our own Steam session keeps RunningAppID alive, which is why the game used to stay "running".
/// It never calls SteamClient.Shutdown(): it just exits, releasing the Steam session cleanly.
/// </summary>
public static class SteamWorker
{
    private static readonly string LogPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "fanfara-log.txt");

    private static void Log(string m)
    {
        try { File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss}  [worker] {m}{Environment.NewLine}"); } catch { }
    }

    public static int Run(uint appId)
    {
        Log($"init {appId}");
        try { SteamClient.Init(appId, asyncCallbacks: false); }
        catch (Exception ex) { Log($"init fallito: {ex.Message}"); return 2; }

        try { SteamUserStats.RequestCurrentStats(); } catch { }

        string? installDir = null;
        try { installDir = SteamApps.AppInstallDir(appId); } catch (Exception ex) { Log($"AppInstallDir: {ex.Message}"); }
        Log($"installDir = {(string.IsNullOrEmpty(installDir) ? "(sconosciuto)" : installDir)}");

        var known = new HashSet<string>();
        bool primed = false;
        bool gameSeen = false;
        int missing = 0;

        while (true)
        {
            // Has the real game closed? (independent of our Steam session)
            if (!string.IsNullOrEmpty(installDir))
            {
                if (IsGameRunning(installDir)) { gameSeen = true; missing = 0; }
                else if (gameSeen && ++missing >= 2) { Log("gioco chiuso -> esco"); break; }
            }

            try { SteamClient.RunCallbacks(); } catch { }

            try
            {
                int total = 0;
                foreach (var a in SteamUserStats.Achievements)
                {
                    total++;
                    if (!a.State) continue;
                    if (known.Contains(a.Identifier)) continue;
                    if (!primed) { known.Add(a.Identifier); continue; }

                    known.Add(a.Identifier);
                    string name = string.IsNullOrEmpty(a.Name) ? a.Identifier : a.Name;
                    double pct = a.GlobalUnlocked >= 0 ? a.GlobalUnlocked * 100.0 : 50.0;
                    Log($"SBLOCCO {name} ({pct.ToString("0.0", CultureInfo.InvariantCulture)}%)");
                    Console.Out.WriteLine($"UNLOCK\t{name}\t{pct.ToString("0.0", CultureInfo.InvariantCulture)}");
                    Console.Out.Flush();
                }
                if (!primed && total > 0) { primed = true; Log($"baseline: {total} achievement"); }
            }
            catch { }

            Thread.Sleep(1000);
        }

        return 0;   // no SteamClient.Shutdown(): exiting releases the session and avoids the native crash
    }

    /// <summary>True if any process is running from the game's install folder.</summary>
    private static bool IsGameRunning(string installDir)
    {
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                var f = p.MainModule?.FileName;
                if (f != null && f.StartsWith(installDir, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch { /* access denied / exited — ignore */ }
            finally { try { p.Dispose(); } catch { } }
        }
        return false;
    }
}

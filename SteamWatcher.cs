using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Steamworks;   // Facepunch.Steamworks

namespace Fanfara;

/// <summary>
/// Watches Steam for the running game and for freshly unlocked achievements.
/// This build writes a diagnostic log to  %USERPROFILE%\fanfara-log.txt  so we can see
/// exactly what happens (running AppID, Steam init, achievement scan, new unlocks).
/// </summary>
public class SteamWatcher
{
    public event Action<string, double>? AchievementUnlocked;

    private CancellationTokenSource? _cts;
    private readonly int _pollMs;
    private uint _currentApp;
    private uint _failedApp;              // an app whose Init failed; don't hammer it every tick
    private bool _primed;                 // baseline of already-unlocked achievements established?
    private int _tick;
    private readonly HashSet<string> _known = new();

    private static readonly string LogPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "fanfara-log.txt");

    public SteamWatcher(int pollMs = 1000) => _pollMs = pollMs;

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss}  {msg}{Environment.NewLine}"); } catch { }
    }

    public void Start()
    {
        try { File.WriteAllText(LogPath, $"{DateTime.Now:HH:mm:ss}  Fanfara watcher avviato{Environment.NewLine}"); } catch { }
        _cts = new CancellationTokenSource();
        Task.Run(() => Loop(_cts.Token));
    }

    public void Stop() { _cts?.Cancel(); TryShutdown(); }

    private async Task Loop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                uint app = ReadRunningAppId();

                if (app == 0)
                {
                    if (_currentApp != 0) { Log($"gioco chiuso (era {_currentApp})"); Detach(); }
                    _failedApp = 0;
                }
                else if (app != _currentApp && app != _failedApp)
                {
                    Log($"RunningAppID -> {app}");
                    Attach(app);
                }

                if (_currentApp != 0)
                {
                    SteamClient.RunCallbacks();
                    ScanAchievements();
                }

                if (++_tick % 10 == 0)
                    Log($"heartbeat: app={_currentApp} primed={_primed} known={_known.Count}");
            }
            catch (Exception ex) { Log($"errore loop: {ex.GetType().Name}: {ex.Message}"); }

            try { await Task.Delay(_pollMs, ct); } catch { }
        }
    }

    private static uint ReadRunningAppId()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        var v = key?.GetValue("RunningAppID");
        return v is int i && i > 0 ? (uint)i : 0u;
    }

    private void Attach(uint app)
    {
        Detach();
        try
        {
            SteamClient.Init(app, asyncCallbacks: false);
            _currentApp = app;
            _failedApp = 0;
            _primed = false;
            _known.Clear();
            try { SteamUserStats.RequestCurrentStats(); }
            catch (Exception ex) { Log($"RequestCurrentStats: {ex.Message}"); }
            Log($"agganciato a {app}, SteamClient.IsValid={SteamClient.IsValid}");
        }
        catch (Exception ex)
        {
            _currentApp = 0;
            _failedApp = app;   // stop retrying this app every second
            Log($"Init({app}) FALLITO: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void Detach() { TryShutdown(); _currentApp = 0; _primed = false; _known.Clear(); }
    private void TryShutdown() { try { if (_currentApp != 0) SteamClient.Shutdown(); } catch { } }

    private void ScanAchievements()
    {
        int total = 0, unlocked = 0;
        var fresh = new List<(string id, string name, double pct)>();

        foreach (var a in SteamUserStats.Achievements)
        {
            total++;
            if (!a.State) continue;         // not unlocked
            unlocked++;

            if (_known.Contains(a.Identifier)) continue;

            if (!_primed) { _known.Add(a.Identifier); continue; }  // baseline: record, don't announce

            _known.Add(a.Identifier);
            string name = string.IsNullOrEmpty(a.Name) ? a.Identifier : a.Name;
            double pct = a.GlobalUnlocked >= 0 ? a.GlobalUnlocked * 100.0 : 50.0;
            fresh.Add((a.Identifier, name, pct));
        }

        if (!_primed && total > 0)
        {
            _primed = true;
            Log($"baseline: {total} achievement, {unlocked} già sbloccati");
        }

        foreach (var f in fresh)
        {
            Log($"SBLOCCO {f.id} nome='{f.name}' global={f.pct:0.0}%");
            AchievementUnlocked?.Invoke(f.name, f.pct);
        }
    }
}

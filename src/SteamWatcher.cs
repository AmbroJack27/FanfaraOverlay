using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Steamworks;   // Facepunch.Steamworks

namespace Fanfara;

/// <summary>
/// Watches Steam for the currently-running game and for freshly unlocked achievements,
/// then raises <see cref="AchievementUnlocked"/> with the achievement's display name and
/// its global unlock percentage (0..100), which the UI turns into a rarity tier.
///
/// Detection approach (no API key needed):
///   1. Read the running game's AppID from HKCU\Software\Valve\Steam\RunningAppID.
///   2. Initialise the Steamworks API *as that game* and read its achievements.
///   3. Poll for state changes; anything newly unlocked fires the event.
///
/// The exact Facepunch.Steamworks member names can vary slightly by version — the spots that
/// may need a small tweak on your machine are marked with TODO(steam).
/// </summary>
public class SteamWatcher
{
    public event Action<string, double>? AchievementUnlocked;

    private CancellationTokenSource? _cts;
    private int _pollMs;
    private uint _currentApp;
    private readonly HashSet<string> _known = new();

    public SteamWatcher(int pollMs = 1000) => _pollMs = pollMs;

    public void Start()
    {
        _cts = new CancellationTokenSource();
        Task.Run(() => Loop(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        TryShutdown();
    }

    private async Task Loop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                uint app = ReadRunningAppId();

                if (app == 0 && _currentApp != 0)
                    Detach();
                else if (app != 0 && app != _currentApp)
                    Attach(app);

                if (_currentApp != 0)
                {
                    SteamClient.RunCallbacks();
                    ScanAchievements();
                }
            }
            catch { /* keep the watcher alive; a game closing mid-poll is normal */ }

            await Task.Delay(_pollMs, ct).ContinueWith(_ => { });
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
            // Init the Steam API as the running game.
            SteamClient.Init(app, asyncCallbacks: false);   // TODO(steam): signature may be Init(app)
            _currentApp = app;
            _known.Clear();

            // Ask Steam for the current stats and the global rarity percentages.
            SteamUserStats.RequestCurrentStats();                       // TODO(steam)
            _ = SteamUserStats.RequestGlobalAchievementPercentages();   // TODO(steam): returns Task

            // Snapshot already-unlocked achievements so we don't re-announce old ones.
            foreach (var a in SteamUserStats.Achievements)              // TODO(steam): Achievements enumerable
                if (a.State) _known.Add(a.Identifier);
        }
        catch
        {
            _currentApp = 0;
        }
    }

    private void Detach()
    {
        TryShutdown();
        _currentApp = 0;
        _known.Clear();
    }

    private void TryShutdown()
    {
        try { if (_currentApp != 0) SteamClient.Shutdown(); } catch { }
    }

    private void ScanAchievements()
    {
        foreach (var a in SteamUserStats.Achievements)
        {
            if (!a.State) continue;                 // not unlocked
            if (_known.Contains(a.Identifier)) continue;

            _known.Add(a.Identifier);

            string name = string.IsNullOrEmpty(a.Name) ? a.Identifier : a.Name;  // TODO(steam): display name
            double percent = a.GlobalUnlocked >= 0 ? a.GlobalUnlocked * 100.0 : 50.0; // TODO(steam): 0..1

            AchievementUnlocked?.Invoke(name, percent);
        }
    }
}

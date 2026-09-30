using System.Windows;

namespace Fanfara;

public partial class App : Application
{
    private OverlayWindow? _overlay;
    private SteamWatcher? _steam;
    private Config _config = Config.Load();

    private void OnStartup(object sender, StartupEventArgs e)
    {
        // The transparent, click-through, always-on-top overlay that renders the notifications.
        _overlay = new OverlayWindow(_config);
        _overlay.Show();

        // Watches Steam for the running game and for freshly unlocked achievements.
        _steam = new SteamWatcher();
        _steam.AchievementUnlocked += (name, percent) =>
        {
            // Marshal to the UI thread and ask the web layer to show a notification.
            _overlay?.Dispatcher.Invoke(() => _overlay.ShowAchievement(name, percent, _config));
        };
        _steam.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _steam?.Stop();
        base.OnExit(e);
    }
}

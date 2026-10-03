# 🎉 Fanfara

**🇬🇧 English** · [🇮🇹 Leggi in italiano](README.it.md)

**Make Steam achievement unlocks as spectacular as on Xbox and PlayStation.**

On Steam, unlocking an achievement shows a small, anonymous notification in the bottom-right
corner. Fanfara replaces it with a celebratory animation whose **sound and style change based
on the achievement's rarity**: the rarer it is (fewer players worldwide have earned it), the
more epic the notification.

It's a **lightweight, native Windows overlay**: it won't slow down your PC and needs no hours
of setup. Launch it, play, and it takes care of the rest.

---

## ⬇️ Download and use (everyone)

1. Go to the **[Releases](../../releases)** section here on GitHub.
2. Download the **`Fanfara.zip`** file of the latest version.
3. Right-click the zip → **Extract All**.
4. Open the extracted folder and double-click **`Fanfara.exe`**.

Nothing to install: no .NET, no terminal. Everything is bundled in the package.

> ⚠️ **The first time**, Windows may show the blue "Windows protected your PC" warning. This is
> normal for free apps that aren't signed with a paid certificate.
> Click **"More info"** → **"Run anyway"**.

On first launch the **settings** window opens: choose the notification style, the sound, the
on-screen position and the duration, and try them right away with the **Prova** (Test) button.
After you close it, Fanfara keeps running with an icon near the clock (bottom-right): click
there to reopen settings or to quit.

Then start Steam, play, and the fanfare will appear on your first unlocked achievement. 🎮

### Tip: turn off Steam's own notification
To avoid duplicates, you can disable Steam's default one:
**Steam → Settings → In Game** (or **Notifications**) → turn off the achievement-unlock notification.

---

## ✨ What's included

- **15 notification styles** — Console, Minimal, Arcade, Neon, Loot, Terminal, Pixel, Ribbon,
  League-of-Legends-style fantasy shields, plus 3D versions of each.
- **28 sounds** — from melodic and cinematic to 8-bit, epic orchestral, and gamer fanfares.
- **5 rarity tiers** that change color, intensity and effects: Common, Uncommon, Rare, Epic,
  Legendary.
- **7 on-screen positions** to choose from.

---

## 🛠️ For developers (build it yourself)

You only need the **.NET 8 SDK** (free): https://dotnet.microsoft.com/download

In the `src/` folder:

```bat
dotnet run
```

to run it in development, or to produce the distributable package:

```bat
dotnet publish -c Release
```

The finished app appears in `src/bin/Release/.../publish/`. It is *self-contained* (it bundles
the .NET runtime), so the folder is standalone and portable to any Windows PC.

> The app must run **on the PC where Steam runs**. Fanfara hooks into the running game by
> reading its AppID from the registry and using the Steamworks API as that game (no API key).

### Publishing a new version
The repo includes `.github/workflows/build.yml`. When you publish a **Release** with a **tag**
starting with `v` (e.g. `v0.1.0`), GitHub builds the app on its own Windows servers and attaches
`Fanfara.zip` to the Release automatically. No manual compiling.

---

## ⚙️ How it works (in short)

- `SteamWatcher.cs` — reads the running game's AppID from the registry
  (`HKCU\Software\Valve\Steam\RunningAppID`), initializes the Steamworks API as that game, and
  checks every second whether a new achievement was unlocked. For each one it gets the worldwide
  percentage (rarity) and raises an event.
- `OverlayWindow` — a transparent, always-on-top, *click-through* window (it doesn't intercept
  mouse clicks) that covers the screen and hosts a WebView2.
- `web/notify.html` — the notification graphics: receives the data from the C# shell and draws
  the unlock in the chosen style and sound, with rarity deciding colors and effects.
- `web/settings.html` — the settings window.
- `config.json` — the saved preferences, next to the exe.

### Honest notes
- The overlay appears over games in **borderless window** mode (today's standard). True
  *exclusive fullscreen* is the only case where it might not show — the same limit as every
  similar overlay.
- Sounds are currently **synthesized live**: they define the character and can be replaced with
  real audio samples later.

## License
MIT — see `LICENSE`. Free to use, modify and share.

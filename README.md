# 🎉 Fanfara

<p align="center">
  <img src="media/fanfara-demo.gif" alt="Fanfara — demo" width="820">
</p>

**🇬🇧 English** · [🇮🇹 Leggi in italiano](README.it.md)

**Make Steam achievement unlocks as spectacular as on Xbox and PlayStation.**

On Steam, unlocking an achievement shows a small, anonymous notification in the corner. Fanfara
replaces it with a celebratory, animated notification whose **style, icon and sound change based
on the achievement's rarity**: the rarer it is (fewer players worldwide have earned it), the more
epic the unlock.

It's a **lightweight, native Windows overlay**: it won't slow down your PC and needs no hours of
setup. Launch it, play, and it takes care of the rest.

---

## ⬇️ Download and use (everyone)

1. Go to the **[Releases](../../releases)** section here on GitHub.
2. Download the latest version:
   - **`FanfaraSetup.exe`** — the installer (recommended): it sets everything up and keeps the app
     **up to date automatically**.
   - or **`Fanfara.zip`** — the portable version: extract it and run `Fanfara.exe`, nothing to install.
3. Launch it.

Nothing else to install: no .NET, no terminal. Everything is bundled.

> ⚠️ **The first time**, Windows may show the blue "Windows protected your PC" warning. This is
> normal for free apps that aren't signed with a paid certificate.
> Click **"More info"** → **"Run anyway"**.

On first launch the **Settings** window opens: pick a style, choose the on-screen position and the
duration, and preview it instantly with the **Prova** (Test) button. Each style already comes with
its own sound — nothing else to configure. After you close the window, Fanfara keeps running with
an icon near the clock (bottom-right): click there to reopen settings or to quit.

Then start Steam, play, and the fanfare appears on your first unlocked achievement. 🎮

### Tip: turn off Steam's own notification
To avoid duplicates, you can disable Steam's default one:
**Steam → Settings → In Game** (or **Notifications**) → turn off the achievement-unlock notification.

---

## ✨ What's included

- **46 notification styles**, organized in **7 tabs**:
  **Base · Console · Capcom · Square-Enix · Microsoft · Ubisoft · CD Projekt**.
- Every style has its **own box**, a **different icon for each rarity** and a **built-in sound** —
  all **3D and animated**. (No separate sound picker: the sound is part of the style.)
- **10 original abstract Base styles**: Onda, Pulsar, Mosaico, Numero, Carica, Coriandoli, Nebula,
  Tratto, Piega, Orbita.
- Game-inspired packs with **original emblems and sounds** (no official logos or audio): Capcom,
  Square-Enix, Microsoft/Xbox, Ubisoft, CD Projekt (The Witcher, Cyberpunk 2077…).
- **5 rarity tiers** that change icon, color, intensity and effects — Common, Uncommon, Rare, Epic,
  Legendary — and **Legendary always has something extra** ✨.
- **5 languages** — English, Italian, French, German and Spanish. The app follows your Windows
  language automatically, or you can pick one in Settings; **everything is translated**, down to
  each style's themed rank ladder. The installer follows your system language too.
- **7 on-screen positions**, adjustable duration, **light/dark theme**, **start with Windows** and
  **automatic updates**.
- **Big, easy-to-read** notifications, great on large and high-resolution monitors.

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

The finished app is *self-contained* (it bundles the .NET runtime), so the folder is standalone
and portable to any Windows PC.

> The app must run **on the PC where Steam runs**. Fanfara talks to the running game through the
> Steamworks API as that game (no API key needed).

### Publishing a new version
The repo includes `.github/workflows/build.yml`. When you publish a **Release** with a **tag**
starting with `v` (e.g. `v0.1.9`), GitHub builds the app on its own Windows servers and attaches
`Fanfara.zip` **and** `FanfaraSetup.exe` to the Release automatically. No manual compiling.

---

## ⚙️ How it works (in short)

- `SteamWatcher.cs` — the supervisor. It launches a **separate Steam worker process** for the
  running game, so the main app never loads Steam directly (this keeps Fanfara stable and avoids
  the game appearing "still running" after you quit).
- `SteamWorker.cs` — the child process. It initializes the Steamworks API **as the running game**,
  checks every second for newly unlocked achievements (reading each one's worldwide rarity %), and
  detects when the game closes. It reports unlocks back to the main app.
- `OverlayWindow` — a transparent, always-on-top, *click-through* window that covers the screen and
  hosts a WebView2.
- `web/notify.html` — the notification graphics: 46 styles that draw the unlock with the right box,
  icon and sound; rarity decides colors and effects.
- `web/settings.html` — the Settings window (with a themed title bar).
- `Updater.cs` — checks GitHub for a newer Release and updates automatically.
- `StartupManager.cs` — optional start with Windows. `config.json` — saved preferences, next to the exe.

### Honest notes
- The overlay appears over games in **borderless window** mode (today's standard). True *exclusive
  fullscreen* is the only case where it might not show — the same limit as every similar overlay.
- Sounds are **synthesized live** inside each style: they define the character and can be swapped
  for recorded audio later.

> ℹ️ Fan project, not affiliated with or endorsed by the game companies. All trademarks belong to
> their respective owners.

## License
MIT — see `LICENSE`. Free to use, modify and share.

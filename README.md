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
setup. Launch it, play, and it takes care of the rest. It also keeps a **local History** of every
achievement you unlock and turns your whole Steam back-catalogue into a **Collection** of trading
cards — built automatically, with real icons, dates and rarity, no login and no setup.

---

## ⬇️ Download and use (everyone)

1. Go to the **[Releases](../../releases)** section here on GitHub.
2. Download the latest version:
   - **`FanfaraSetup.exe`** — the installer (recommended): it sets everything up and keeps the app
     **up to date automatically** (updates are **verified with a SHA-256 checksum** before they run).
   - or **`Fanfara.zip`** — the portable version: extract it and run `Fanfara.exe`, nothing to install.
3. Launch it.

Nothing else to install: no .NET, no terminal. Everything is bundled.

> ⚠️ **The first time**, Windows may show the blue "Windows protected your PC" warning. This is
> normal for free apps that aren't signed with a paid certificate.
> Click **"More info"** → **"Run anyway"**.

Fanfara opens in **one window** with a short logo intro, then a **Home** with four areas:
**Personalizza** (styles & settings), **Cronologia** (your unlock History), **Collezione** (your
card Collection) and **Guida** (the in-app guide). Everything happens inside this single window —
no extra windows pop up. Pick a style, choose the on-screen position and the duration, and preview
it instantly with the **Prova** (Test) button. Each style already comes with its own sound —
nothing else to configure. Close the window and Fanfara keeps running with an icon near the clock
(bottom-right): click there to reopen it or to quit.

Then start Steam, play, and the fanfare appears on your first unlocked achievement. 🎮

### Tip: turn off Steam's own notification
To avoid duplicates, you can disable Steam's default one:
**Steam → Settings → In Game** (or **Notifications**) → turn off the achievement-unlock notification.

---

## ✨ What's included

- **Notification styles organized in 8 tabs**:
  **Base · Console · Fantasy · Capcom · Square-Enix · Microsoft · Ubisoft · CD Projekt**.
- Every style has its **own box**, a **different icon for each rarity** and a **built-in sound** —
  all **3D and animated**. (No separate sound picker: the sound is part of the style.)
- **10 original abstract Base styles**: Onda, Pulsar, Mosaico, Numero, Carica, Coriandoli, Nebula,
  Tratto, Piega, Orbita.
- Game-inspired packs with **original emblems and sounds** (no official logos or audio): Capcom,
  Square-Enix, Microsoft/Xbox, Ubisoft, CD Projekt (The Witcher, Cyberpunk 2077…).
- **🕒 Cronologia (History)** — a local, searchable log of every achievement you unlock: grouped by
  day, with a **filter by year**, rarity chips and search, plus a stats summary. Paginated (25 per
  page) so it stays fast even with thousands of entries.
- **🃏 Collezione (Collection)** — your entire Steam achievement catalogue as **animated trading
  cards** with real icons, descriptions and rarity, each with a subtle 3D tilt. Paginated too.
- **☁️ Automatic Steam import & sync** — the first time, Fanfara **imports all your past Steam
  achievements** (real icons, unlock dates and worldwide rarity) with **no login and no API key to
  enter**. It refreshes periodically and captures new unlocks live, so History and Collection stay
  current on their own. It works through a tiny personal proxy that keeps the API key private.
- **5 rarity tiers** that change icon, color, intensity and effects — Common, Uncommon, Rare, Epic,
  Legendary — and **Legendary always has something extra** ✨.
- **5 languages** — English, Italian, French, German and Spanish. The app follows your Windows
  language automatically, or you can pick one in Settings; **everything is translated**, down to
  each style's themed rank ladder. The installer follows your system language too.
- **7 on-screen positions**, adjustable duration, **light/dark theme everywhere**, **start with
  Windows** and **automatic updates** (SHA-256 verified).
- **Works over fullscreen & borderless games** — Fanfara automatically prepares your Steam games so notifications show up in-game, with no setup.
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
> Steamworks API as that game (no API key needed). The History/Collection backfill uses the Steam
> **Web** API read-only, through the proxy in `proxy/` (see below), so end users never enter a key.

### Publishing a new version
The repo includes `.github/workflows/build.yml`. When you publish a **Release** with a **tag**
starting with `v` (e.g. `v0.2.0`), GitHub builds the app on its own Windows servers and attaches
`Fanfara.zip`, `FanfaraSetup.exe` **and** `FanfaraSetup.exe.sha256` (the checksum the auto-updater
verifies) to the Release automatically. No manual compiling.

---

## ⚙️ How it works (in short)

- `SteamWatcher.cs` — the supervisor. It launches a **separate Steam worker process** for the
  running game, so the main app never loads Steam directly (this keeps Fanfara stable and avoids
  the game appearing "still running" after you quit). It also respawns the worker if it crashes.
- `SteamWorker.cs` — the child process. It initializes the Steamworks API **as the running game**,
  checks every second for newly unlocked achievements (reading each one's worldwide rarity %), and
  detects when the game closes. It reports unlocks back to the main app.
- `OverlayWindow` — a transparent, always-on-top, *click-through* window that covers the screen and
  hosts a WebView2.
- `web/notify.html` — the notification graphics: the styles that draw the unlock with the right box,
  icon and sound; rarity decides colors and effects.
- **The single-window UI** — one WebView2 that navigates between `web/home.html` (logo intro +
  Home), `web/settings.html` (styles & settings), `web/history.html` (Cronologia),
  `web/collection.html` (Collezione) and `web/guide.html` (the guide). Every page follows the
  light/dark theme and the chosen language.
- `SteamId.cs` — finds your SteamID64 locally (from Steam's registry keys / `loginusers.vdf`), so
  the import needs nothing from you.
- `SteamHistory.cs` + `AchievementStore.cs` — the backfill and the local store. They fetch your
  owned games and per-game achievements (icons, unlock dates, rarity) in parallel and save them to
  `history.json`, de-duplicated and kept up to date.
- `proxy/worker.js` — a tiny **Cloudflare Worker** that proxies the Steam Web API and **keeps the
  API key as a private secret**, so the app (and every user) works with zero configuration.
- `Updater.cs` — checks GitHub for a newer Release, **downloads the installer, verifies it against
  the published SHA-256 checksum**, and only then runs it (it aborts on a mismatch).
- `StartupManager.cs` — optional start with Windows. `config.json` — saved preferences, next to the exe.

### Honest notes
- The overlay appears over games in **borderless window** mode (today's standard). True *exclusive
  fullscreen* is the only case where it might not show — the same limit as every similar overlay.
- Games that use **Frame Generation** or **true exclusive fullscreen** may hide any external overlay. Turn on the **advanced "Frame Generation compatibility"** option in Settings (needs admin rights and a one-time restart) to make notifications show there too.
- Sounds are **synthesized live** inside each style: they define the character and can be swapped
  for recorded audio later.
- The History/Collection import reads **public** Steam data. If your Steam profile (game details) is
  private, there's nothing to import until you make it public.

> ℹ️ Fan project, not affiliated with or endorsed by the game companies. All trademarks belong to
> their respective owners.

## License
MIT — see `LICENSE`. Free to use, modify and share.

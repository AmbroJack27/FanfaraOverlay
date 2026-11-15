
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
cards — built automatically, with real icons, dates and rarity, no login and no setup. It even
ranks you against your Steam friends and the world by how many games you've completed 100%.

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

Fanfara opens in **one window** with a short logo intro, then a **Home** that shows your achievement
life at a glance: your current fanfare (live, with a **Test** button), your latest unlocks with
their real icons, your rank in the **World** and **Friends** leaderboards, your rarest cards, the
last 7 days and the games you're about to finish 100%. Every tile opens its section —
**Personalizza** (styles & settings), **Cronologia** (History), **Collezione** (Collection),
**Classifica** (leaderboard) and **Guida** (guide) — all inside this single window, no extra
windows pop up. In Personalizza, styles are grouped in big category boxes: hover a style to watch
it animate, press **With sound** to hear it, click to pick it and **Save**. Each style already comes
with its own sound — nothing else to configure. Close the window and Fanfara keeps running with an icon near the clock
(bottom-right): click there to reopen it or to quit.

Then start Steam, play, and the fanfare appears on your first unlocked achievement. 🎮

### Tip: turn off Steam's own notification
To avoid duplicates, you can disable Steam's default one:
**Steam → Settings → In Game** (or **Notifications**) → turn off the achievement-unlock notification.

---

## ✨ What's included

- **🧭 Guided setup** — on first launch (and once after updating) a step-by-step guide checks that
  Steam is running and that your achievements are **visible** (if Steam's privacy hides them, it shows
  exactly which settings to make Public, with a button to Steam's privacy page), lets you **test the
  notification** and choose the **screen** to show it on, explains how to set games' **display mode**,
  sets start with Windows, volume and updates, and runs the first import with live progress. You can
  reopen it any time from the **Guided setup** button at the top of Home, the Guide or the tray menu.
- **🖥️ Multiple screens** — notifications appear on the screen where you're playing (or on the one you
  choose in the guided setup), not always on the main one.
- **🔒 Clear privacy warning** — if Steam hides your achievements, the Home shows a fixed red notice
  with *How to fix it*; notifications keep working and Fanfara retries the import every 30 minutes.
- **🏠 A Home that's all about you** — one screen with your fanfare, latest unlocks, leaderboard
  rank (World/Friends), rarest cards, the last 7 days and the games closest to 100%. It fits any
  window size and opens sized to your screen.
- **Notification styles organized in 7 categories** with **live animated previews**:
  **Base (including Fantasy) · Console · Capcom · Square-Enix · Microsoft · Ubisoft · CD Projekt**,
  plus a search box.
- Every style has its **own box**, a **different icon for each rarity** and a **built-in sound** —
  all **3D and animated**. (No separate sound picker: the sound is part of the style.)
- **10 original abstract Base styles**: Onda, Pulsar, Mosaico, Numero, Carica, Coriandoli, Nebula,
  Tratto, Piega, Orbita.
- Game-inspired packs with **original emblems and sounds** (no official logos or audio): Capcom,
  Square-Enix, Microsoft/Xbox, Ubisoft, CD Projekt (The Witcher, Cyberpunk 2077…).
- **🕒 Cronologia (History)** — a local **timeline** of every achievement you unlock: one line per
  unlock with its time and a dot in the rarity colour, while **epic and legendary** ones become big
  cards with the game's artwork. On top: today's summary, stats and a **16-week calendar** (the more
  you unlocked, the brighter the day). On the side: the **month calendar** (browse months, click a
  day to see only that day), the month's rarity mix and its top games. Search, rarity chips and a
  date filter (last 7 days, this month, a year); days load a week at a time so it stays fast.
- **🃏 Collezione (Collection)** — your entire Steam achievement catalogue as **trading cards**: a
  metallic frame in the rarity colour, rarity gems, the real icon over the game artwork, and effects
  that grow with rarity (light sweep, holographic foil, sparkles). **Click a card** and it lifts off,
  spins and lands in front of you (with a soft sound); **flip it** to see the story of that unlock:
  how many players in 1,000 have it, date and time, its place in your collection, your progress in
  the game, the «completed 100%» ribbon and **which of your Fanfara friends have it — and who got it
  first**. Two buttons under the opened card: **Copy image** (paste it anywhere with Ctrl+V) and
  **Post to the showcase**, which posts the card to #bacheca-trofei on the Fanfara Discord server
  after checking on Steam that the achievement is really yours. The page opens with your **collector
  level** (rare cards are worth more points); new unlocks since your last visit arrive in a **pack**
  you tear open; a lit **showcase** holds your 5 rarest cards. Below, **one album per game** (search,
  sort, Covers or Shelf view): each album is an **old leather-bound book** you open yourself — clasp,
  gold tooling, sewn binding, cards held by photo corners in unlock order, empty «?» frames for what's
  missing, a bookplate with cards, completion, **hours played**, rarest and first card, and pages
  you turn (click, corner or arrow keys). From the showcase, **See it in its album** jumps to the card.
- **☁️ Automatic Steam import & sync** — the first time, Fanfara **imports all your past Steam
  achievements** (real icons, unlock dates and worldwide rarity) with **no login and no API key to
  enter**. It captures new unlocks live, **refreshes a game as soon as you close it** and runs a full
  check every 12 hours even while it stays open. It works through a tiny personal proxy that keeps
  the API key private.
- **🏆 Classifica (Leaderboard)** — a ranking by **number of games completed 100%**: your completed
  games shown as cards, a public **World** board (opt-in) and a **Friends** board that compares you
  with the Steam friends who use Fanfara. Each copy of Fanfara computes its own score locally — no
  login, nothing to configure.
- **💬 Send feedback** — a button on the Home, in the Guide and in the tray menu opens a short form
  (problem / idea / other). It goes straight to the developer with only Fanfara's version, language
  and Windows version; for problems you can attach the technical log, cleaned of personal data.
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
  Home, fed by `HomeData.cs`), `web/settings.html` (styles & settings, list in `web/styles.js`), `web/history.html` (Cronologia),
  `web/collection.html` (Collezione), `web/classifica.html` (the leaderboard) and `web/guide.html`
  (the guide), plus `web/welcome.html` (the guided setup: Steam/privacy check, notification test and
  screen choice, display-mode tips, preferences, first import). Every page follows the light/dark theme and the chosen language.
- `SteamId.cs` — finds your SteamID64 locally (from Steam's registry keys / `loginusers.vdf`), so
  the import needs nothing from you.
- `SteamHistory.cs` + `AchievementStore.cs` — the backfill and the local store. They fetch your
  owned games and per-game achievements (icons, unlock dates, rarity) in parallel and save them to
  `history.json`, de-duplicated and kept up to date.
- `proxy/worker.js` — a tiny **Cloudflare Worker** that proxies the Steam Web API and **keeps the
  API key as a private secret**, so the app (and every user) works with zero configuration.
- `Leaderboard.cs` + `GamesStore.cs` — the online leaderboard. Each app finds your 100%-completed
  games locally and registers your score through the proxy into a Cloudflare **D1** database; the
  World and Friends rankings just read from it. There is **no per-game server computation**, so no
  Steam rate-limit problems.
  `GamesStore` (`games.json`) also keeps each game's minutes played, read from Steam during the
  sync, for the first page of its album in the Collection.
- `CardShare.cs` — posts a card image to the proxy (`POST /share`); the proxy verifies the unlock on
  Steam and forwards it to the Discord showcase channel (secret `DISCORD_SHOWCASE_WEBHOOK`).
- `Feedback.cs` — sends the in-app feedback form to the proxy (`POST /feedback`), which stores it in
  the D1 database (only a one-way hash of the SteamID is kept, for an anonymous daily limit).
- `Updater.cs` — checks GitHub for a newer Release, **downloads the installer, verifies it against
  the published SHA-256 checksum**, and only then runs it (it aborts on a mismatch).
- `StartupManager.cs` — optional start with Windows. `config.json` — saved preferences, next to the exe.

### Honest notes
- The overlay appears over games in **Fullscreen / borderless** mode (today's standard). In
  **«Exclusive fullscreen»** Windows lets nothing draw over the game: you hear the fanfare but don't
  see it. Fix: in the game's video options pick **«Fullscreen»** without exclusive (sometimes called
  «Borderless»). The game still runs at the refresh rate set in Windows (e.g. 165 Hz). Fanfara
  notices when a game was in exclusive mode and reminds you when you close it.
- Some games with **Frame Generation** (DLSS 3, FSR 3) may hide external overlays even in
  «Fullscreen». Turn on the **advanced "Frame Generation compatibility"** option in Settings (needs
  admin rights and a one-time restart) to make notifications show there too.
- Sounds are **synthesized live** inside each style: they define the character and can be swapped
  for recorded audio later.
- The History/Collection import reads **public** Steam data. If your Steam profile (game details) is
  private, there's nothing to import until you make it public.
- The **Friends** leaderboard shows the friends who **also use Fanfara** (each app reports its own
  score): as more of your friends install it, more of them appear.

> ℹ️ Fan project, not affiliated with or endorsed by the game companies. All trademarks belong to
> their respective owners.

## License
MIT — see `LICENSE`. Free to use, modify and share.

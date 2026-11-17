# Contributing to Fanfara

Thanks for your interest in Fanfara! 🎉 This is a free, open-source hobby project, and contributions
of all kinds are welcome — bug reports, ideas, new notification styles, translations, or code.

> 🇮🇹 Puoi scrivere issue e pull request **in italiano o in inglese**, come preferisci. Riassunto in
> italiano [in fondo alla pagina](#-in-breve-italiano).

## Ways to help

- **Report a bug** — use **Send feedback** in the app (it can attach the cleaned-up log), or open an
  [issue](../../issues) with the *Bug report* template.
- **Suggest an idea** — a new style, a feature, a sound — use the *Feature request* template.
- **Improve a translation** — interface text lives in `src/web/i18n.js` and in each page's own
  string table; the Guide is in `src/web/guide.js`. Every text exists in **Italian, English, French,
  German and Spanish**.
- **Add a notification style** — see [below](#adding-a-notification-style).
- **Fix or improve the code** — see [the project map](#project-map).

## Building it yourself

You only need the **.NET 8 SDK** (free): <https://dotnet.microsoft.com/download>

In the `src/` folder:

```bat
dotnet run
```

runs it in development, and

```bat
dotnet publish -c Release
```

builds the self-contained package. The app must run **on the PC where Steam runs**.

## Project map

| Path | What it is |
|---|---|
| `src/App.xaml.cs` | Start-up, tray icon, sync scheduling |
| `src/SteamWatcher.cs`, `SteamWorker.cs` | Live unlock detection in a separate process (Steamworks) |
| `src/SteamHistory.cs`, `AchievementStore.cs`, `GamesStore.cs` | Import of past achievements and local stores |
| `src/OverlayWindow.*` + `src/web/notify.html` | The notification window and its graphics |
| `src/SettingsWindow.*` | The main window (one WebView2) and the page ↔ app messages |
| `src/WebLock.cs` | Turns off zoom, F12 and browser shortcuts in every WebView2 |
| `src/Leaderboard.cs`, `CollectionShare.cs`, `CardShare.cs`, `Feedback.cs` | Everything that talks to the proxy |
| `src/FsOpt.cs`, `Mpo.cs` | Fullscreen preparation and the Frame Generation option |
| `src/Updater.cs` | Self-update with SHA-256 check |
| `src/web/home.html` | Home |
| `src/web/settings.html` + `styles.js` | Customize and the style catalog |
| `src/web/history.html` | History (timeline) |
| `src/web/collection.html` | Collection, albums, cards, friends' collections |
| `src/web/classifica.html` | Leaderboard |
| `src/web/welcome.html` | Guided setup |
| `src/web/guide.html` + `guide.js` | In-app Guide |
| `src/web/feedback.html` | Send feedback |
| `proxy/worker.js` | The Cloudflare Worker ([`proxy/README.md`](proxy/README.md)) |
| `Fanfara.iss` | The Windows installer script (Inno Setup) |
| `.github/workflows/build.yml` | CI: builds and publishes a Release on a `v*` tag |

## Adding a notification style

There are **47 styles in 7 categories**. A new one needs:

1. its graphics, emblems for the 5 rarities and sound in `src/web/notify.html`;
2. an entry in `FAN_STYLES` and in a category of `FAN_CATS` in `src/web/styles.js`;
3. its rank names in the 5 languages in `src/web/i18n.js`.

Styles inspired by games must use **original emblems and sounds** — no official logos, artwork or
audio.

## Rules of the house

- **Light and dark theme**: every page and every new element must look right in both.
- **Five languages**: any new text is added in it / en / fr / de / es, and new features are explained
  in the Guide.
- **Bilingual docs**: README, release notes and docs are written in English and Italian.
- **Free tools only**, and never commit secrets (keys, webhook URLs): they live as Cloudflare or
  GitHub secrets.

## Making a pull request

1. **Fork** the repository and create a branch for your change.
2. Keep changes **focused** — one topic per pull request.
3. Make sure the app still **builds** (`dotnet build -c Release`) before opening the PR.
4. Describe **what** you changed and **why** (the PR template will guide you).
5. By contributing, you agree your contribution is licensed under the project's
   [MIT License](LICENSE).

## Releases

Publishing a **Release** with a tag starting with `v` (e.g. `v0.5.3`) makes GitHub build the app and
attach `Fanfara.zip`, `FanfaraSetup.exe` and `FanfaraSetup.exe.sha256` automatically. The
«🇮🇹 Novità» section of the release notes is announced on Discord. No manual compiling.

## Code of conduct

Please be kind and respectful. This project follows the
[Contributor Covenant](CODE_OF_CONDUCT.md).

---

## 🇮🇹 In breve (italiano)

- **Come aiutare**: segnala problemi con **Invia feedback** nell'app o con una issue, proponi idee,
  migliora le traduzioni (`i18n.js`, le tabelle di testo delle pagine, `guide.js` — sempre in 5
  lingue), aggiungi stili o correggi il codice.
- **Compilare**: serve il **.NET 8 SDK**; in `src/` `dotnet run` per provarla, `dotnet publish -c
  Release` per il pacchetto. Deve girare sul PC dove gira Steam.
- **Nuovo stile**: grafica, emblemi e suono in `notify.html`, voce in `styles.js`, nomi dei gradi in
  `i18n.js`. Solo **emblemi e suoni originali**.
- **Regole**: tema chiaro e scuro ovunque, testi in 5 lingue e spiegati nella Guida, documenti in
  inglese e italiano, solo strumenti gratuiti, mai segreti nel codice.
- **Pull request**: un argomento per volta, controlla che compili, spiega cosa e perché. Licenza MIT.
- **Versioni**: una Release con tag `v…` fa compilare tutto a GitHub e annuncia la sezione
  «🇮🇹 Novità» su Discord.

# Contributing to Fanfara

Thanks for your interest in Fanfara! 🎉 This is a free, open-source, hobby project, and
contributions of all kinds are welcome — bug reports, ideas, new notification styles, translations,
or code.

> 🇮🇹 Puoi scrivere issue e pull request **in italiano o in inglese**, come preferisci.

## Ways to help

- **Report a bug** — open an [issue](../../issues) using the *Bug report* template.
- **Suggest an idea** — a new style, a feature, a sound — use the *Feature request* template.
- **Improve a translation** — all interface and notification text lives in `src/web/i18n.js`
  (Italian, English, French, German, Spanish). Fixes and new languages are very welcome.
- **Add a notification style** — styles live in `src/web/notify.html`.
- **Fix or improve the code** — see below.

## Building it yourself

You only need the **.NET 8 SDK** (free): <https://dotnet.microsoft.com/download>

In the `src/` folder:

```bat
dotnet run            # run in development
dotnet publish -c Release   # produce the distributable package
```

The app must run **on the PC where Steam runs**. See the [README](README.md) for how it works.

## Making a pull request

1. **Fork** the repository and create a branch for your change.
2. Keep changes **focused** — one topic per pull request.
3. Make sure the app still **builds** (`dotnet build -c Release`) before opening the PR.
4. Describe **what** you changed and **why** (the PR template will guide you).
5. By contributing, you agree your contribution is licensed under the project's
   [MIT License](LICENSE).

## Project layout (quick map)

- `src/` — the C#/WPF app (overlay, settings, Steam watcher, updater).
- `src/web/notify.html` — the notification graphics (46 styles).
- `src/web/settings.html` — the settings window.
- `src/web/i18n.js` — all translations (interface, labels, per-style ranks).
- `Fanfara.iss` — the Windows installer script.
- `.github/workflows/build.yml` — CI that builds and publishes a Release on a `v*` tag.

## Releases

Publishing a **Release** with a tag starting with `v` (e.g. `v0.1.10`) makes GitHub build the app
and attach `Fanfara.zip` and `FanfaraSetup.exe` automatically. No manual compiling.

## Code of conduct

Please be kind and respectful. This project follows the
[Contributor Covenant](CODE_OF_CONDUCT.md).

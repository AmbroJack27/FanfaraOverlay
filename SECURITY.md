# Security Policy

> 🇮🇹 Puoi segnalare un problema di sicurezza **in italiano o in inglese**. Trovi un riassunto in
> italiano [in fondo alla pagina](#-in-breve-italiano).

## Supported versions

Only the **latest release** of Fanfara is supported. Please update to the newest version
(the app updates itself, or download it from the [Releases](../../releases) page) before reporting
an issue.

| Version        | Supported |
| -------------- | --------- |
| Latest release | ✅        |
| Older versions | ❌        |

## Reporting a vulnerability

**Please do not open a public issue for security problems.**

Instead, report it privately:

1. Go to the repository's **Security** tab → **Report a vulnerability**
   (GitHub Private Vulnerability Reporting), **or**
2. Contact the maintainer **[@AmbroJack27](https://github.com/AmbroJack27)** privately.

Please include:

- what the problem is and how serious you think it is,
- the steps to reproduce it,
- the Fanfara version and your Windows version.

You can expect an acknowledgement as soon as possible. Once the issue is confirmed and fixed, a new
release will be published and the fix noted in the release notes. Thank you for helping keep Fanfara
and its users safe. 🙏

## Scope / good to know

Fanfara is a Windows app with a small online service. On your PC it:

- talks to the **running game** through the Steamworks API as that game (no API key, no account
  credentials, no Steam login);
- stores its settings in `config.json` next to the app, and your history and collection in
  `%AppData%\Fanfara` (`history.json`, `games.json`);
- checks GitHub for updates, downloads the official installer from the Releases page and **verifies
  its SHA-256 checksum** before running it;
- never asks for passwords, and never sends your files or the log unless you attach it to a feedback
  yourself (personal details are removed first).

Its online part is a **Cloudflare Worker** (see [`proxy/`](proxy/README.md)) that keeps the Steam
Web API key secret. It receives your **SteamID** (a public identifier) and stores:

| What | When | Who can see it |
|---|---|---|
| Name, avatar and 100%-completed games | Always, for the Friends leaderboard | Your Steam friends who use Fanfara |
| The same, on the World leaderboard | **Only if you join**; leaving removes you from it | Everyone |
| A light copy of your collection | When "Share my collection with friends" is on (default); turning it off deletes it | **Only your Steam friends** |
| Feedback and showcase posts | Only what you send | The maintainer / the Fanfara Discord |

The Worker only reads **public** Steam data, keeps its secrets (Steam key, Discord webhooks) as
Cloudflare secrets — never in the code — and limits how often each player can write.

Things we especially want to hear about: ways to read a non-friend's collection, to write data as
another SteamID, to bypass the update checksum, or to make the app run code it shouldn't.

---

## 🇮🇹 In breve (italiano)

- È supportata solo **l'ultima versione**. Aggiorna prima di segnalare.
- **Non aprire una issue pubblica** per problemi di sicurezza: usa la scheda **Security → Report a
  vulnerability** oppure contatta **[@AmbroJack27](https://github.com/AmbroJack27)** in privato,
  con descrizione, passi per riprodurlo, versione di Fanfara e di Windows.
- Sul tuo PC Fanfara parla con il gioco tramite Steamworks (niente chiave, niente login), salva tutto
  in locale e **verifica lo SHA-256** di ogni aggiornamento prima di avviarlo.
- La parte online (un Worker Cloudflare) riceve il tuo **SteamID** pubblico e conserva: nome, avatar e
  giochi al 100% per la classifica Amici; la classifica Mondiale **solo se entri**; una copia leggera
  della collezione visibile **solo ai tuoi amici di Steam** (spegnendo la condivisione viene cancellata);
  feedback e post in bacheca solo se li invii tu.
- Ci interessano soprattutto: leggere la collezione di chi non è tuo amico, scrivere dati a nome di un
  altro SteamID, aggirare il controllo degli aggiornamenti o far eseguire codice non previsto.

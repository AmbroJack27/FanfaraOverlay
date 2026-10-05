# Security Policy

> 🇮🇹 Puoi segnalare un problema di sicurezza **in italiano o in inglese**.

## Supported versions

Only the **latest release** of Fanfara is supported. Please update to the newest version
(the app updates itself automatically, or download the latest from the
[Releases](../../releases) page) before reporting an issue.

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

Fanfara is a local Windows overlay. It:

- talks to the **running game** through the Steamworks API as that game (no API key, no account
  credentials),
- checks GitHub for updates and downloads the official installer from the Releases page,
- stores its settings in a local `config.json` next to the executable.

It does not collect or transmit personal data.

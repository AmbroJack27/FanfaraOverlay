# Fanfara Steam proxy (Cloudflare Worker)

A tiny read-only proxy that lets Fanfara rebuild a user's Steam achievement history
**without shipping a Steam Web API key inside the public app**. The key stays here as a
server-side secret; the app only sends a SteamID (a public identifier) and gets back
public data (owned games, unlocked achievements + unlock time, global rarity %).

No personal data is stored. Responses are cached at the edge for a short time to stay well
within Steam's rate limits. It runs on Cloudflare's free tier (100k requests/day).

---

## One-time setup

### 1. Get a Steam Web API key (personal, free)
- Go to https://steamcommunity.com/dev/apikey
- Sign in with **your personal** Steam account, register a key (domain can be anything, e.g. `localhost`).
- Copy the key — it's a 32-character hex string. Keep it private.

### 2. Install the Cloudflare tool and log in
```bash
npm install -g wrangler
wrangler login            # opens the browser to authorize a free Cloudflare account
```

### 3. Deploy
From this `proxy/` folder:
```bash
wrangler deploy
```
Wrangler prints the public URL, e.g. `https://fanfara-steam.<your-subdomain>.workers.dev`.

### 4. Store the key as a secret (never in the code)
```bash
wrangler secret put STEAM_KEY
# paste the Steam Web API key when prompted
```

### 5. Point Fanfara at it
Put the Worker URL into Fanfara's config as `SyncProxyUrl`
(e.g. `https://fanfara-steam.<your-subdomain>.workers.dev`).

---

## Endpoints

- `GET /owned?steamid=<17 digits>`
  → `{ ok, private, count, games:[{ appid, name, playtime }] }`
  `private:true` means the profile's **Game details** are not public.

- `GET /ach?steamid=<17 digits>&appid=<id>&l=<language>`
  → `{ ok, appid, game, achievements:[{ api, name, desc, unlock, percent }] }`
  `unlock` is a unix timestamp; `percent` is the global unlock % (rarity), or null.

- `GET /health` → `{ ok:true }`

## Notes
- The user must have their Steam profile **Game details = Public** for Steam to return their
  data (this is a Steam rule, independent of the key). Fanfara detects `private:true` and
  guides the user; otherwise it falls back to tracking only new unlocks from now on.
- To rotate the key later: `wrangler secret put STEAM_KEY` again.
- This proxy is only for history backfill. Live achievement pop-ups are 100% local and keep
  working even if the Worker is offline.

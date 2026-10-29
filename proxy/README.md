# Fanfara Steam proxy + leaderboard (Cloudflare Worker)

A tiny read-only proxy that lets Fanfara rebuild a user's Steam achievement history
**without shipping a Steam Web API key inside the public app**, plus a **100%-completion
leaderboard** (ranking by number of games completed 100%). The key stays here as a
server-side secret; the app only sends a SteamID (a public identifier).

Runs on Cloudflare's **free tier** (Worker + D1 database + a once-a-minute cron job).

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

### 3. Create the leaderboard database (D1)
From this `proxy/` folder:
```bash
wrangler d1 create fanfara-lb
```
Copy the printed **database_id** into `wrangler.toml` (replace `PASTE-DATABASE-ID-HERE`).
Then create the tables:
```bash
wrangler d1 execute fanfara-lb --file=schema.sql --remote
```

### 4. Deploy
```bash
wrangler deploy
```
Wrangler prints the public URL, e.g. `https://fanfara-steam.<your-subdomain>.workers.dev`.

### 5. Store the key as a secret (never in the code)
```bash
wrangler secret put STEAM_KEY
# paste the Steam Web API key when prompted
```

### 6. Point Fanfara at it
Put the Worker URL into Fanfara's config as `SyncProxyUrl`.

---

## Endpoints

### Proxy (history / collection)
- `GET /owned?steamid=<17 digits>`
  → `{ ok, private, count, games:[{ appid, name, playtime }] }`

- `GET /ach?steamid=<17 digits>&appid=<id>&l=<language>`
  → `{ ok, appid, game, total, unlocked, achievements:[{ api, name, desc, unlock, percent, icon }] }`
  `total`/`unlocked` let the app detect 100%-completed games at no extra cost.

### Leaderboard (100% completed games)
- `GET /lb/join?steamid=&l=&perfect=&total=` — opt **in** to the public world board (stores a provisional score; the server verifies it in the background).
- `GET /lb/leave?steamid=` — opt **out**: deletes the row completely.
- `GET /lb/submit?steamid=&perfect=&total=` — refresh your own provisional score (members only).
- `GET /lb/world?steamid=&limit=` → `{ ok, you:{rank,perfect,total}, top:[{rank,you,name,avatar,perfect,total,verified}] }`
- `GET /lb/friends?steamid=&l=` → `{ ok, friendsPrivate, friends:[{rank,you,name,avatar,perfect,total,ready}], pending }`

- `GET /health` → `{ ok:true, leaderboard:true }`

## How the score is computed (and why it's background)
A player's score is **number of games completed 100%** (all achievements unlocked), with total
unlocked achievements as the tie-breaker. The authoritative score is always computed **server-side**
from public Steam data, so it can't be faked. Because a free Worker can make only ~50 Steam calls per
request and a full scan needs one call per owned game, the compute runs in the **background** (the
cron job processes a chunk of games at a time and caches the result in D1). The app also submits the
number it already computed locally, shown immediately as a *provisional* value until the server
confirms it. Scores refresh at most once every 24h.

## Privacy
- Scores are derived from **public** Steam data. A player appears in the public **world** board only
  after an explicit opt-in (`/lb/join`); `/lb/leave` removes their row entirely.
- The **friends** board is computed from public data and returned only to the requesting user. It
  needs the requester's **friends list** to be public; otherwise it returns `friendsPrivate:true`.
- The profile's **Game details** must be **Public** for Steam to return achievements (a Steam rule,
  independent of the key).

## Notes
- To rotate the key later: `wrangler secret put STEAM_KEY` again.
- Live achievement pop-ups are 100% local and keep working even if the Worker is offline.
- Inspect the database any time, e.g. `wrangler d1 execute fanfara-lb --command "SELECT COUNT(*) FROM scores" --remote`.

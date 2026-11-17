# Fanfara online service (Cloudflare Worker)

> 🇮🇹 Riassunto in italiano [in fondo alla pagina](#-in-breve-italiano).

A small Worker that gives Fanfara everything it needs online **without shipping a Steam Web API key
inside the public app**:

- a **read-only Steam proxy** to import a player's achievement history;
- the **100%-completion leaderboards** (Friends and World);
- **friends' collections**;
- **feedback** and the **showcase** (card posts on the Fanfara Discord).

The app only ever sends a **SteamID** (a public identifier). The Steam key and the Discord webhooks are
**Cloudflare secrets**, never in the code. Everything runs on Cloudflare's **free tier** (one Worker +
one D1 database, no cron job).

Live pop-ups are 100% local: they keep working even if the Worker is offline.

---

## One-time setup

### 1. Steam Web API key (personal, free)
Go to <https://steamcommunity.com/dev/apikey>, sign in with **your personal** Steam account and
register a key (any domain, e.g. `localhost`). Keep it private.

### 2. Cloudflare tool
```bash
npm install -g wrangler
wrangler login
```

### 3. Database (D1)
```bash
wrangler d1 create fanfara-lb
```
Copy the printed `database_id` into [`wrangler.toml`](wrangler.toml), then create the tables with
[`schema.sql`](schema.sql):
```bash
wrangler d1 execute fanfara-lb --file=schema.sql --remote
```
(The Worker also creates `collections`, `feedback` and `shares` by itself if they are missing.)

### 4. Deploy and add the secrets
```bash
wrangler deploy
wrangler secret put STEAM_KEY                  # the Steam Web API key
wrangler secret put DISCORD_WEBHOOK            # optional: feedback → a private Discord channel
wrangler secret put DISCORD_SHOWCASE_WEBHOOK   # optional: "Post to showcase" → the showcase channel
```
Paste each value when asked — never in a file, never in chat.

The GitHub release announcement uses a separate **repository secret**, `DISCORD_RELEASE_WEBHOOK`
(Settings → Secrets and variables → Actions).

### 5. Point Fanfara at it
The Worker URL goes in Fanfara's `config.json` as `SyncProxyUrl`.

---

## Endpoints

All `steamid` values are 17-digit SteamID64s; anything else is refused.

### Steam proxy
| Endpoint | Returns |
|---|---|
| `GET /owned?steamid=` | `{ ok, private, count, games:[{ appid, name, playtime }] }` |
| `GET /ach?steamid=&appid=&l=` | `{ ok, appid, game, total, unlocked, achievements:[{ api, name, desc, unlock, percent, icon }] }` — or `private:true` when Steam hides the profile's achievements, or `nostats` / `why` when a game has none |

Good answers are cached for up to an hour; "private" answers are never cached, so a player who fixes their
privacy sees the change right away.

### Leaderboards
Scores (**games completed 100%**, with total achievements as tie-breaker) are **computed by each
player's own Fanfara** and registered here; names and avatars are refreshed from Steam.

| Endpoint | What it does |
|---|---|
| `GET /lb/submit?steamid=&perfect=&total=` | Registers / refreshes your score (used by the Friends board) |
| `GET /lb/join?steamid=&perfect=&total=` | Also shows you on the public **World** board |
| `GET /lb/leave?steamid=` | Removes you from the World board (friends still see you) |
| `GET /lb/world?steamid=&limit=` | `{ ok, you:{ rank, perfect, total }, top:[…] }` |
| `GET /lb/friends?steamid=` | Your Steam friends who use Fanfara, ranked, with `steamid` and `cards` (shared collection size); `friendsPrivate:true` if your friends list is private |
| `GET /lb/friends-ach?steamid=&appid=` | Which of your Fanfara friends unlocked achievements in a game, and when (back of a card) |

### Friends' collections
| Endpoint | What it does |
|---|---|
| `POST /coll/put` `{ steamid, cards, data }` | Stores your collection: compact JSON → gzip → base64 (≈150 KB for 1,700 cards; max 3,000,000 characters; at most one upload a minute) |
| `POST /coll/del` `{ steamid }` | Deletes it (sharing turned off) |
| `GET /coll/get?steamid=&friend=` | A friend's collection with their name and avatar — **only if you are Steam friends**; otherwise `notfriend`, `friendsPrivate` or `none` |

### Feedback and showcase
| Endpoint | What it does |
|---|---|
| `POST /feedback` | Stores a feedback and forwards it to `DISCORD_WEBHOOK`; max 10 a day per player |
| `POST /share` | Checks on Steam that the achievement is really yours, then posts the card to `DISCORD_SHOWCASE_WEBHOOK`; max 10 a day, no duplicates |

### Health
`GET /health` → `{ ok:true, service:"fanfara-steam", leaderboard:true }`

## Privacy
- Only **public** Steam data is read. The profile's **Game details** must be Public for Steam to
  return achievements (a Steam rule, independent of the key).
- The **World** board shows a player only after an explicit join; leaving removes them from it.
- The **Friends** board and **collections** are returned only to the player's Steam friends; this
  needs the requester's friends list to be public.
- Feedback and showcase rate limits use an anonymous hash, not the SteamID itself.

## Notes
- Rotate the key: `wrangler secret put STEAM_KEY` again.
- Look inside the database:
  `wrangler d1 execute fanfara-lb --remote --command "SELECT COUNT(*) FROM collections"`.

---

## 🇮🇹 In breve (italiano)

- È il piccolo servizio online di Fanfara su **Cloudflare** (piano gratuito, un Worker e un database
  D1): legge da Steam i dati **pubblici** con una chiave che resta segreta, tiene le classifiche
  Amici e Mondiale, le collezioni degli amici, i feedback e la bacheca su Discord.
- L'app invia solo lo **SteamID** (pubblico). Chiave Steam e webhook di Discord sono **segreti di
  Cloudflare** (`STEAM_KEY`, `DISCORD_WEBHOOK`, `DISCORD_SHOWCASE_WEBHOOK`); l'annuncio delle
  versioni usa il segreto di GitHub `DISCORD_RELEASE_WEBHOOK`. Mai nel codice, mai in chat.
- I punteggi sono **calcolati da ogni Fanfara sul proprio PC** e registrati qui; non c'è nessun
  processo pianificato.
- La collezione di un giocatore si legge **solo se siete amici su Steam**; spegnendo la condivisione
  viene cancellata. La classifica Mondiale mostra solo chi entra.
- Le notifiche dal vivo sono tutte locali: funzionano anche se il servizio è spento.

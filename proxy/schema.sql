-- Fanfara online service — Cloudflare D1 (SQLite) schema.
-- Run once after creating the database:  npx wrangler d1 execute fanfara-lb --file=schema.sql --remote
--
--   scores       — each player's 100%-completion score, computed by their own Fanfara and registered
--                  by the Worker (/lb/submit, /lb/join). member=1 = also shown on the public World board;
--                  /lb/leave sets member=0 (friends still see the player).
--   collections  — friends' collections (/coll/put, /coll/get, /coll/del)
--   feedback     — "Send feedback" messages (/feedback)
--   shares       — showcase posts, to avoid duplicates and limit them per day (/share)
--
-- The Worker also creates collections, feedback and shares by itself the first time they are used,
-- so on an existing database running this file again is harmless.

CREATE TABLE IF NOT EXISTS scores (
  steamid  TEXT PRIMARY KEY,          -- SteamID64 (17 digits)
  name     TEXT NOT NULL DEFAULT '',  -- public Steam persona name
  avatar   TEXT NOT NULL DEFAULT '',  -- public Steam avatar URL
  perfect  INTEGER NOT NULL DEFAULT 0,-- number of games completed 100%
  total    INTEGER NOT NULL DEFAULT 0,-- total unlocked achievements (tie-breaker)
  member   INTEGER NOT NULL DEFAULT 0,-- 1 = shown on the public World leaderboard
  verified INTEGER NOT NULL DEFAULT 0,-- legacy column, no longer used
  updated  INTEGER NOT NULL DEFAULT 0,-- epoch seconds of the last score update
  perf     TEXT                       -- 0.6.0: games at 100% as JSON [[appid,name,completedAt],...] (the Worker adds it to older databases)
);

-- Fast ordering for the World board.
CREATE INDEX IF NOT EXISTS idx_board ON scores(member, perfect DESC, total DESC);

CREATE TABLE IF NOT EXISTS collections (
  steamid TEXT PRIMARY KEY,           -- owner's SteamID64
  ts      INTEGER NOT NULL,           -- epoch seconds of the last upload
  cards   INTEGER NOT NULL,           -- number of cards
  data    TEXT NOT NULL               -- compact JSON → gzip → base64
);

CREATE TABLE IF NOT EXISTS feedback (
  id INTEGER PRIMARY KEY AUTOINCREMENT, ts INTEGER NOT NULL, kind TEXT, text TEXT, contact TEXT,
  version TEXT, lang TEXT, os TEXT, log TEXT,
  who TEXT                            -- anonymous hash, only for the daily limit
);

CREATE TABLE IF NOT EXISTS shares (
  id INTEGER PRIMARY KEY AUTOINCREMENT, ts INTEGER NOT NULL,
  who TEXT,                           -- anonymous hash
  appid INTEGER, api TEXT
);

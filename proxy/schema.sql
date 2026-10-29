-- Fanfara leaderboard — Cloudflare D1 (SQLite) schema.
-- Run once after creating the database:  npx wrangler d1 execute fanfara-lb --file=schema.sql --remote
--
-- Only two tables:
--   scores  — the computed 100%-completion score for a SteamID (and whether it opted into the public board)
--   queue   — SteamIDs waiting for a (re)compute by the background cron job
-- All data here is derived from PUBLIC Steam data. A player is in the public world board only when member=1,
-- and /lb/leave deletes their row entirely.

CREATE TABLE IF NOT EXISTS scores (
  steamid  TEXT PRIMARY KEY,          -- SteamID64 (17 digits)
  name     TEXT NOT NULL DEFAULT '',  -- public Steam persona name
  avatar   TEXT NOT NULL DEFAULT '',  -- public Steam avatar URL
  perfect  INTEGER NOT NULL DEFAULT 0,-- number of games completed 100%
  total    INTEGER NOT NULL DEFAULT 0,-- total unlocked achievements (tie-breaker)
  member   INTEGER NOT NULL DEFAULT 0,-- 1 = opted into the public world leaderboard
  verified INTEGER NOT NULL DEFAULT 0,-- 1 = computed server-side (not just client-submitted)
  updated  INTEGER NOT NULL DEFAULT 0 -- epoch seconds of the last score update
);

-- Fast ordering for the world board.
CREATE INDEX IF NOT EXISTS idx_board ON scores(member, perfect DESC, total DESC);

CREATE TABLE IF NOT EXISTS queue (
  steamid TEXT PRIMARY KEY,           -- SteamID waiting for a background (re)compute
  cursor  INTEGER NOT NULL DEFAULT 0, -- next game index to process (resumes across cron ticks)
  perfect INTEGER NOT NULL DEFAULT 0, -- accumulator while a compute is in progress
  total   INTEGER NOT NULL DEFAULT 0, -- accumulator while a compute is in progress
  apps    TEXT NOT NULL DEFAULT '',   -- frozen JSON list of appids for this run
  added   INTEGER NOT NULL DEFAULT 0  -- epoch seconds the job was enqueued (oldest first)
);

// Fanfara Steam proxy + 100%-completion leaderboard (friends who use Fanfara + public world board).
//
// Two jobs:
//
// 1) PROXY: let the Fanfara desktop app rebuild a user's unlocked-achievement history WITHOUT
//    shipping a Steam Web API key inside the (public) app. The key lives here as a server-side
//    secret (STEAM_KEY). Read-only public Steam data only.
//      GET /owned?steamid=                 -> the user's owned games
//      GET /ach?steamid=&appid=&l=         -> unlocked achievements for one game (+ total/unlocked)
//
// 2) LEADERBOARD (ranking by number of games completed 100%). Each Fanfara user computes their OWN
//    score locally (the app already scans all their games) and registers it here — so there is NO
//    per-game server computation and NO Steam rate-limit problem.
//      GET /lb/submit?steamid=&perfect=&total=   -> register/refresh my score (so friends can see me)
//      GET /lb/join?steamid=&perfect=&total=     -> also appear in the PUBLIC world board (opt-in)
//      GET /lb/leave?steamid=                     -> leave the public world board (stay visible to friends)
//      GET /lb/world?steamid=&limit=              -> the public world ranking (+ my rank)
//      GET /lb/friends?steamid=                    -> my Steam friends who use Fanfara, ranked (private to me)
//      GET /lb/friends-ach?steamid=&appid=         -> which of those friends unlocked what in ONE game, and when
//
// 3) FEEDBACK from the app's «Send feedback» form.
//      POST /feedback  {kind, text, contact, version, lang, os, log, steamid}
//      Stored in the D1 table `feedback` (created automatically). The SteamID is NOT stored: only a
//      one-way hash of it, used to stop accidental floods (max 10 messages a day per person).
//      Read them in Cloudflare → D1 → your database → Console:  SELECT * FROM feedback ORDER BY id DESC;
//      If the secret DISCORD_WEBHOOK is set, every message is ALSO posted to that (private) Discord
//      channel, with the technical log attached as a .txt file.
//
// 4) SHOWCASE: «Post to the showcase» on a Collection card.
//      POST /share  multipart: meta (JSON: steamid, appid, api, side, lang…) + image (PNG of the card)
//      The achievement is checked on Steam (it must really be unlocked on that profile), then the card
//      is posted to #bacheca-trofei through the secret DISCORD_SHOWCASE_WEBHOOK. Each card can be
//      posted once, max 10 cards a day per person (D1 table `shares`, created automatically; only a
//      one-way hash of the SteamID is stored).
//
// A player's score is their number of games completed 100%, with total unlocked achievements as the
// tie-breaker. Names/avatars are public Steam data, fetched here when a score is registered.
//
// Privacy: the world board is public and opt-in only (member=1). The friends board shows a user's
// score only to their own Steam friends who also use Fanfara, and only for users who have registered.

const STEAM = 'https://api.steampowered.com';
const WORLD_DEFAULT = 100;
const WORLD_MAX = 200;

export default {
  async fetch(request, env, ctx) {
    const url = new URL(request.url);
    if (url.pathname === '/feedback') {
      if (request.method !== 'POST') return json({ ok: false, error: 'method' }, 405);
      if (!env.DB) return json({ ok: false, error: 'nodb' }, 503);
      try { return await feedback(request, env, ctx); }
      catch (e) { return json({ ok: false, error: 'server' }, 500); }
    }
    if (url.pathname === '/share') {
      if (request.method !== 'POST') return json({ ok: false, error: 'method' }, 405);
      if (!env.DB) return json({ ok: false, error: 'nodb' }, 503);
      if (!env.DISCORD_SHOWCASE_WEBHOOK) return json({ ok: false, error: 'noshowcase' }, 503);
      try { return await share(request, env); }
      catch (e) { return json({ ok: false, error: 'server' }, 500); }
    }
    if (request.method !== 'GET') return json({ ok: false, error: 'method' }, 405);

    if (url.pathname === '/' || url.pathname === '/health') {
      return json({ ok: true, service: 'fanfara-steam', leaderboard: !!env.DB }, 200);
    }

    const steamid = url.searchParams.get('steamid') || '';
    if (!isId(steamid)) return json({ ok: false, error: 'steamid' }, 400);

    const lang = (url.searchParams.get('l') || 'italian').replace(/[^a-z]/gi, '').slice(0, 20) || 'italian';

    try {
      // ---- proxy (history / collection) ----
      if (url.pathname === '/owned') {
        return await withCache(request, ctx, () => owned(steamid, env));
      }
      if (url.pathname === '/ach') {
        const appid = url.searchParams.get('appid') || '';
        if (!/^\d{1,8}$/.test(appid)) return json({ ok: false, error: 'appid' }, 400);
        return await withCache(request, ctx, () => ach(steamid, appid, lang, env));
      }

      // ---- leaderboard ----
      if (url.pathname.startsWith('/lb/')) {
        if (!env.DB) return json({ ok: false, error: 'nodb' }, 503);
        if (url.pathname === '/lb/submit')  return await lbSubmit(steamid, url, env);
        if (url.pathname === '/lb/join')    return await lbJoin(steamid, url, env);
        if (url.pathname === '/lb/leave')   return await lbLeave(steamid, env);
        if (url.pathname === '/lb/world')   return await lbWorld(steamid, url, env);
        if (url.pathname === '/lb/friends') return await lbFriends(steamid, env);
        if (url.pathname === '/lb/friends-ach') {
          const appid = url.searchParams.get('appid') || '';
          if (!/^\d{1,8}$/.test(appid)) return json({ ok: false, error: 'appid' }, 400);
          return await lbFriendsAch(steamid, appid, env);
        }
      }

      return json({ ok: false, error: 'not_found' }, 404);
    } catch (e) {
      return json({ ok: false, error: 'upstream' }, 502);
    }
  }
};

// ---------------------------------------------------------------------------
// helpers
// ---------------------------------------------------------------------------

function isId(s) { return /^\d{17}$/.test(s); }
function now() { return Math.floor(Date.now() / 1000); }
function clampInt(v, lo, hi) { const n = parseInt(v, 10); if (!Number.isFinite(n)) return 0; return Math.max(lo, Math.min(hi, n)); }

function json(obj, status = 200, maxAge = 0) {
  return new Response(JSON.stringify(obj), {
    status,
    headers: {
      'content-type': 'application/json; charset=utf-8',
      'cache-control': maxAge ? ('public, max-age=' + maxAge) : 'no-store'
    }
  });
}

async function withCache(request, ctx, producer) {
  const cache = caches.default;
  const hit = await cache.match(request);
  if (hit) return hit;
  const res = await producer();
  // Only answers marked cacheable (max-age) are stored: a "profile is private" answer must never stick.
  if (res.status === 200 && /max-age=\d+/.test(res.headers.get('cache-control') || '')) ctx.waitUntil(cache.put(request, res.clone()));
  return res;
}

// Public persona name + avatar for up to 100 SteamIDs. Returns a map steamid -> {name, avatar}.
async function summaries(ids, env) {
  const map = {};
  for (let i = 0; i < ids.length; i += 100) {
    const batch = ids.slice(i, i + 100).join(',');
    const u = `${STEAM}/ISteamUser/GetPlayerSummaries/v2/?key=${env.STEAM_KEY}&steamids=${batch}&format=json`;
    const j = await fetch(u).then(r => r.json()).catch(() => ({}));
    const arr = j && j.response && j.response.players;
    if (Array.isArray(arr)) for (const p of arr) map[p.steamid] = { name: p.personaname || '', avatar: p.avatarfull || p.avatarmedium || '' };
  }
  return map;
}

// Public friend list (needs the user's friends list to be public). Returns [] otherwise.
// ---------------------------------------------------------------------------
// feedback
// ---------------------------------------------------------------------------

const FEEDBACK_PER_DAY = 10;
async function sha16(text) {
  const d = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(text));
  return [...new Uint8Array(d)].slice(0, 8).map(b => b.toString(16).padStart(2, '0')).join('');
}
function clip(v, n) { return String(v == null ? '' : v).replace(/\u0000/g, '').trim().slice(0, n); }

async function feedback(request, env, ctx) {
  const len = Number(request.headers.get('content-length') || 0);
  if (len > 64000) return json({ ok: false, error: 'big' }, 413);
  let b;
  try { b = await request.json(); } catch (e) { return json({ ok: false, error: 'json' }, 400); }
  if (!b || typeof b !== 'object') return json({ ok: false, error: 'json' }, 400);

  const kind = ['bug', 'idea', 'other'].includes(b.kind) ? b.kind : 'other';
  const text = clip(b.text, 4000);
  if (text.length < 3) return json({ ok: false, error: 'empty' }, 400);

  // who = anonymous hash (SteamID if known, else the connecting IP) — only for the daily limit
  const raw = isId(String(b.steamid || '')) ? 'sid:' + b.steamid : 'ip:' + (request.headers.get('cf-connecting-ip') || '');
  const who = await sha16(raw + ':fanfara-feedback');

  await env.DB.prepare(`CREATE TABLE IF NOT EXISTS feedback (
      id INTEGER PRIMARY KEY AUTOINCREMENT, ts INTEGER NOT NULL, kind TEXT, text TEXT, contact TEXT,
      version TEXT, lang TEXT, os TEXT, log TEXT, who TEXT)`).run();

  const t = now();
  const c = await env.DB.prepare('SELECT COUNT(*) AS n FROM feedback WHERE who = ? AND ts > ?').bind(who, t - 86400).first();
  if (c && c.n >= FEEDBACK_PER_DAY) return json({ ok: false, error: 'limit' }, 429);

  await env.DB.prepare('INSERT INTO feedback (ts, kind, text, contact, version, lang, os, log, who) VALUES (?,?,?,?,?,?,?,?,?)')
    .bind(t, kind, text, clip(b.contact, 200), clip(b.version, 20), clip(b.lang, 5), clip(b.os, 120), clip(b.log, 30000), who).run();

  // Forward to Discord in the background (the app gets its answer right away).
  if (env.DISCORD_WEBHOOK) {
    const job = toDiscord(env.DISCORD_WEBHOOK, {
      kind, text, contact: clip(b.contact, 200), version: clip(b.version, 20), lang: clip(b.lang, 5),
      os: clip(b.os, 120), log: clip(b.log, 30000), who
    }).catch(() => {});
    if (ctx && ctx.waitUntil) ctx.waitUntil(job); else await job;
  }
  return json({ ok: true });
}

// Posts one feedback as a Discord embed. allowed_mentions is empty so a message can never ping
// @everyone/@here or anyone else, whatever the user typed.
async function toDiscord(hook, f) {
  const look = { bug: ['🐞 Problema', 0xff6b8b], idea: ['💡 Idea', 0xF5C242], other: ['💬 Altro', 0x4C8DFF] }[f.kind] || ['💬 Altro', 0x4C8DFF];
  const fields = [];
  if (f.contact) fields.push({ name: 'Contatto', value: f.contact.slice(0, 1000), inline: false });
  fields.push({ name: 'Versione', value: f.version || '—', inline: true });
  fields.push({ name: 'Lingua', value: f.lang || '—', inline: true });
  fields.push({ name: 'Windows', value: f.os || '—', inline: true });
  const payload = {
    username: 'Fanfara Feedback',
    allowed_mentions: { parse: [] },
    embeds: [{
      title: look[0], color: look[1],
      description: f.text.length > 4000 ? f.text.slice(0, 3990) + '…' : f.text,
      fields, footer: { text: 'utente ' + f.who + (f.log ? ' · registro allegato' : '') },
      timestamp: new Date().toISOString()
    }]
  };
  const form = new FormData();
  form.append('payload_json', JSON.stringify(payload));
  if (f.log) form.append('files[0]', new Blob([f.log], { type: 'text/plain' }), 'registro-fanfara.txt');
  await fetch(hook, { method: 'POST', body: form });
}

// ---------------------------------------------------------------------------
// showcase (#bacheca-trofei)
// ---------------------------------------------------------------------------

const SHARES_PER_DAY = 10;
const TIER = [ // [max global %, label, colour] — same rarity tiers as the app
  [1, '✨ Leggendario', 0xF5C242], [5, '💜 Epico', 0xB078FF], [10, '💎 Raro', 0x4C8DFF], [40, '🟢 Non comune', 0x4ac46a], [101, '⚪ Comune', 0x8A93A6]
];

async function share(request, env) {
  const len = Number(request.headers.get('content-length') || 0);
  if (len > 6 * 1024 * 1024) return json({ ok: false, error: 'big' }, 413);
  let form;
  try { form = await request.formData(); } catch (e) { return json({ ok: false, error: 'form' }, 400); }
  let meta;
  try { meta = JSON.parse(String(form.get('meta') || '{}')); } catch (e) { return json({ ok: false, error: 'meta' }, 400); }
  const img = form.get('image');
  const steamid = String(meta.steamid || ''), appid = String(meta.appid || ''), api = String(meta.api || '');
  if (!isId(steamid) || !/^\d{1,8}$/.test(appid) || !api || api.length > 128) return json({ ok: false, error: 'meta' }, 400);
  if (!img || typeof img === 'string' || img.size < 100 || img.size > 5 * 1024 * 1024) return json({ ok: false, error: 'image' }, 400);
  const lang = ({ it: 'italian', en: 'english', fr: 'french', de: 'german', es: 'spanish' })[String(meta.lang || '')] || 'italian';

  const who = await sha16('sid:' + steamid + ':fanfara-share');
  await env.DB.prepare(`CREATE TABLE IF NOT EXISTS shares (
      id INTEGER PRIMARY KEY AUTOINCREMENT, ts INTEGER NOT NULL, who TEXT, appid INTEGER, api TEXT)`).run();
  const dup = await env.DB.prepare('SELECT id FROM shares WHERE who = ? AND appid = ? AND api = ?').bind(who, Number(appid), api).first();
  if (dup) return json({ ok: false, error: 'already' }, 409);
  const t = now();
  const c = await env.DB.prepare('SELECT COUNT(*) AS n FROM shares WHERE who = ? AND ts > ?').bind(who, t - 86400).first();
  if (c && c.n >= SHARES_PER_DAY) return json({ ok: false, error: 'limit' }, 429);

  // 1) Steam must confirm the unlock on this profile (no fake trophies)
  const pu = `${STEAM}/ISteamUserStats/GetPlayerAchievements/v1/?key=${env.STEAM_KEY}&steamid=${steamid}&appid=${appid}&l=${lang}&format=json`;
  const pr = await fetch(pu);
  if (pr.status === 429 || pr.status >= 500) return json({ ok: false, error: 'rate' }, 503);
  const pj = await pr.json().catch(() => ({}));
  const ps = pj && pj.playerstats;
  const a = ps && Array.isArray(ps.achievements) ? ps.achievements.find(x => x.apiname === api) : null;
  if (!a || a.achieved !== 1) return json({ ok: false, error: 'notyours' }, 403);

  // 2) public extras: player name/avatar and worldwide rarity
  const [who2, gj] = await Promise.all([
    summaries([steamid], env),
    fetch(`${STEAM}/ISteamUserStats/GetGlobalAchievementPercentagesForApp/v2/?gameid=${appid}&format=json`).then(r => r.json()).catch(() => ({}))
  ]);
  const player = who2[steamid] || { name: 'Un giocatore', avatar: '' };
  let pct = null;
  const garr = gj && gj.achievementpercentages && gj.achievementpercentages.achievements;
  if (Array.isArray(garr)) { const g = garr.find(x => x.name === api); if (g) pct = Number(g.percent); }
  const tier = TIER.find(x => (pct == null ? 101 : pct) < x[0]) || TIER[TIER.length - 1];
  const game = clip(ps.gameName || meta.game, 100) || 'Steam';
  const achName = clip(a.name || meta.name || api, 120);
  const pctTxt = pct == null ? '' : (pct < 10 ? pct.toFixed(1) : Math.round(pct)).toString().replace('.', ',') + '% dei giocatori';

  // 3) post to Discord (#bacheca-trofei)
  const payload = {
    username: 'Fanfara',
    allowed_mentions: { parse: [] },
    embeds: [{
      author: { name: clip(player.name, 80) || 'Un giocatore', icon_url: player.avatar || undefined },
      title: '🏆 ' + achName,
      description: '🎮 **' + game + '**' + (pctTxt ? '\n' + tier[1] + ' · ' + pctTxt : ''),
      color: tier[2],
      image: { url: 'attachment://carta.png' },
      footer: { text: 'Condiviso con Fanfara' },
      timestamp: new Date((a.unlocktime || t) * 1000).toISOString()
    }]
  };
  const out = new FormData();
  out.append('payload_json', JSON.stringify(payload));
  out.append('files[0]', new Blob([await img.arrayBuffer()], { type: 'image/png' }), 'carta.png');
  const dr = await fetch(env.DISCORD_SHOWCASE_WEBHOOK, { method: 'POST', body: out });
  if (!dr.ok) return json({ ok: false, error: 'discord' }, 502);

  await env.DB.prepare('INSERT INTO shares (ts, who, appid, api) VALUES (?,?,?,?)').bind(t, who, Number(appid), api).run();
  return json({ ok: true });
}

async function friendIds(steamid, env) {
  const u = `${STEAM}/ISteamUser/GetFriendList/v1/?key=${env.STEAM_KEY}&steamid=${steamid}&relationship=friend&format=json`;
  const j = await fetch(u).then(r => r.json()).catch(() => ({}));
  const arr = j && j.friendslist && j.friendslist.friends;
  if (!Array.isArray(arr)) return [];
  return arr.map(f => f.steamid).filter(isId);
}

// ---------------------------------------------------------------------------
// proxy: owned games + per-game achievements
// ---------------------------------------------------------------------------

async function owned(steamid, env) {
  const u = `${STEAM}/IPlayerService/GetOwnedGames/v1/?key=${env.STEAM_KEY}`
    + `&steamid=${steamid}&include_appinfo=1&include_played_free_games=1&format=json`;
  const r = await fetch(u);
  const j = await r.json().catch(() => ({}));
  const resp = j && j.response;
  if (!resp || !('games' in resp)) {
    return json({ ok: true, private: true, games: [] }, 200, 0);   // never cache: the player may fix it right away
  }
  const games = (resp.games || []).map(g => ({
    appid: g.appid,
    name: g.name || ('App ' + g.appid),
    playtime: g.playtime_forever || 0
  }));
  return json({ ok: true, private: false, count: games.length, games }, 200, 1800);
}

async function ach(steamid, appid, lang, env) {
  const pu = `${STEAM}/ISteamUserStats/GetPlayerAchievements/v1/?key=${env.STEAM_KEY}`
    + `&steamid=${steamid}&appid=${appid}&l=${lang}&format=json`;
  const gu = `${STEAM}/ISteamUserStats/GetGlobalAchievementPercentagesForApp/v2/?gameid=${appid}&format=json`;
  const su = `${STEAM}/ISteamUserStats/GetSchemaForGame/v2/?key=${env.STEAM_KEY}&appid=${appid}&l=${lang}&format=json`;

  const [pr, gr, sr] = await Promise.all([fetch(pu), fetch(gu), fetch(su)]);

  // If Steam RATE-LIMITED the player-achievements call (HTTP not OK), return a NON-cacheable failure
  // (503, not a 200). The app then skips this game and retries later, instead of recording a false
  // "0 unlocked" that would corrupt the 100%-completion count. (A genuine "no achievements" game
  // returns HTTP 200 with success:false and is handled below as a real total:0.)
  // Steam answers HTTP 400 ("Requested app has no stats") for games WITHOUT achievements, and 403 when
  // it won't share them: both are final answers, not "busy" — report a real total:0 so the app stops
  // retrying those games on every sync. Only other errors (429 rate limit, 5xx) are retried.
  // BUT 403 / "Profile is not public" means the player's achievements are hidden by their Steam privacy
  // (profile or game details not Public): that is NOT "no achievements". Say so (never cached), so the
  // app can tell the player how to fix it instead of importing a silent, empty collection.
  if (pr.status === 400 || pr.status === 403) {
    const perr = String(((await pr.json().catch(() => ({}))).playerstats || {}).error || '');
    if (pr.status === 403 || /not public|private/i.test(perr)) {
      return json({ ok: true, appid: Number(appid), total: 0, unlocked: 0, achievements: [], private: true }, 200, 0);
    }
    return json({ ok: true, appid: Number(appid), total: 0, unlocked: 0, achievements: [], nostats: true }, 200, 3600);
  }
  if (!pr.ok) return json({ ok: false, error: 'rate' }, 503);

  const pj = await pr.json().catch(() => ({}));
  const gj = await gr.json().catch(() => ({}));
  const sj = await sr.json().catch(() => ({}));

  const ps = pj && pj.playerstats;
  if (!ps || ps.success === false || !Array.isArray(ps.achievements)) {
    return json({ ok: true, appid: Number(appid), total: 0, unlocked: 0, achievements: [] }, 200, 3600);
  }

  const pct = {};
  const garr = gj && gj.achievementpercentages && gj.achievementpercentages.achievements;
  if (Array.isArray(garr)) for (const a of garr) pct[a.name] = Number(a.percent);

  const icons = {};
  const sarr = sj && sj.game && sj.game.availableGameStats && sj.game.availableGameStats.achievements;
  if (Array.isArray(sarr)) for (const a of sarr) if (a.name) icons[a.name] = a.icon || '';

  const total = ps.achievements.length;
  let unlocked = 0;
  const out = [];
  for (const a of ps.achievements) {
    if (a.achieved !== 1) continue;
    unlocked++;
    out.push({
      api: a.apiname,
      name: a.name || a.apiname,
      desc: a.description || '',
      unlock: a.unlocktime || 0,
      percent: (a.apiname in pct) ? pct[a.apiname] : null,
      icon: icons[a.apiname] || ''
    });
  }
  return json({ ok: true, appid: Number(appid), game: ps.gameName || '', total, unlocked, achievements: out }, 200, 3600);
}

// ---------------------------------------------------------------------------
// leaderboard (scores are computed by each user's own app and registered here)
// ---------------------------------------------------------------------------

// Register/refresh a user's score. `member` 1 = also show in the public world board; when omitted,
// the existing member flag is kept (a plain refresh). Names/avatars are refreshed from Steam.
async function registerScore(steamid, perfect, total, member, env) {
  const sum = await summaries([steamid], env);
  const me = sum[steamid] || { name: '', avatar: '' };
  const t = now();
  if (member === 1) {
    await env.DB.prepare(
      `INSERT INTO scores (steamid,name,avatar,perfect,total,member,updated)
       VALUES (?,?,?,?,?,1,?)
       ON CONFLICT(steamid) DO UPDATE SET name=?,avatar=?,perfect=?,total=?,member=1,updated=?`
    ).bind(steamid, me.name, me.avatar, perfect, total, t, me.name, me.avatar, perfect, total, t).run();
  } else {
    await env.DB.prepare(
      `INSERT INTO scores (steamid,name,avatar,perfect,total,member,updated)
       VALUES (?,?,?,?,?,0,?)
       ON CONFLICT(steamid) DO UPDATE SET name=?,avatar=?,perfect=?,total=?,updated=?`
    ).bind(steamid, me.name, me.avatar, perfect, total, t, me.name, me.avatar, perfect, total, t).run();
  }
}

async function lbSubmit(steamid, url, env) {
  const perfect = clampInt(url.searchParams.get('perfect'), 0, 100000);
  const total = clampInt(url.searchParams.get('total'), 0, 10000000);
  await registerScore(steamid, perfect, total, undefined, env);
  return json({ ok: true, perfect, total });
}

async function lbJoin(steamid, url, env) {
  const perfect = clampInt(url.searchParams.get('perfect'), 0, 100000);
  const total = clampInt(url.searchParams.get('total'), 0, 10000000);
  await registerScore(steamid, perfect, total, 1, env);
  const rank = await worldRank(steamid, env);
  return json({ ok: true, member: true, perfect, total, rank });
}

// Leaving the public world board keeps the row (so friends still see you) — just clears member.
async function lbLeave(steamid, env) {
  await env.DB.prepare('UPDATE scores SET member=0 WHERE steamid=?').bind(steamid).run();
  return json({ ok: true, member: false });
}

async function lbWorld(steamid, url, env) {
  const limit = clampInt(url.searchParams.get('limit'), 1, WORLD_MAX) || WORLD_DEFAULT;
  const res = await env.DB.prepare(
    'SELECT steamid,name,avatar,perfect,total FROM scores WHERE member=1 ORDER BY perfect DESC, total DESC LIMIT ?'
  ).bind(limit).all();
  const rows = (res && res.results) || [];
  const top = rows.map((r, i) => ({
    rank: i + 1, you: r.steamid === steamid,
    name: r.name, avatar: r.avatar, perfect: r.perfect, total: r.total, ready: true
  }));
  const rank = await worldRank(steamid, env);
  return json({ ok: true, you: rank, top });
}

async function worldRank(steamid, env) {
  const me = await env.DB.prepare('SELECT perfect,total,member FROM scores WHERE steamid=?').bind(steamid).first();
  if (!me || !me.member) return null;
  const c = await env.DB.prepare(
    'SELECT COUNT(*) AS n FROM scores WHERE member=1 AND (perfect>? OR (perfect=? AND total>?))'
  ).bind(me.perfect, me.perfect, me.total).first();
  return { rank: (c ? c.n : 0) + 1, perfect: me.perfect, total: me.total };
}

// Friends who use Fanfara (i.e. have a registered score), ranked. Needs the user's friends list public.
// For the back of a Collection card: which of my Steam friends WHO USE FANFARA unlocked achievements
// in this game, and when. Only Fanfara users (rows in `scores`) are asked — a handful of Steam calls,
// one per friend — and SteamIDs never leave the server: the app only gets names, avatars and times.
const FRIENDS_ACH_MAX = 40;   // stays well inside a Worker's sub-request budget
async function lbFriendsAch(steamid, appid, env) {
  const ids = await friendIds(steamid, env);
  if (!ids.length) return json({ ok: true, friendsPrivate: true, friends: [] });

  const known = [];
  for (let i = 0; i < ids.length; i += 100) {
    const batch = ids.slice(i, i + 100);
    const q = 'SELECT steamid,name,avatar FROM scores WHERE steamid IN (' + batch.map(() => '?').join(',') + ')';
    const res = await env.DB.prepare(q).bind(...batch).all();
    for (const r of (res.results || [])) known.push(r);
  }
  const list = known.slice(0, FRIENDS_ACH_MAX);

  const friends = await Promise.all(list.map(async k => {
    const u = `${STEAM}/ISteamUserStats/GetPlayerAchievements/v1/?key=${env.STEAM_KEY}&steamid=${k.steamid}&appid=${appid}&format=json`;
    try {
      const r = await fetch(u);
      if (!r.ok) {
        // 400/403 = doesn't own the game or private profile; anything else = Steam busy (unknown)
        return { name: k.name, avatar: k.avatar, owns: (r.status === 400 || r.status === 403) ? false : null, ach: {} };
      }
      const j = await r.json().catch(() => ({}));
      const ps = j && j.playerstats;
      if (!ps || ps.success === false || !Array.isArray(ps.achievements)) return { name: k.name, avatar: k.avatar, owns: false, ach: {} };
      const ach = {};
      for (const a of ps.achievements) if (a.achieved === 1) ach[a.apiname] = a.unlocktime || 0;
      return { name: k.name, avatar: k.avatar, owns: true, ach };
    } catch (e) {
      return { name: k.name, avatar: k.avatar, owns: null, ach: {} };
    }
  }));
  return json({ ok: true, friendsPrivate: false, friends });
}

async function lbFriends(steamid, env) {
  const ids = await friendIds(steamid, env);
  const all = [steamid, ...ids];

  const known = {};
  for (let i = 0; i < all.length; i += 100) {
    const batch = all.slice(i, i + 100);
    const q = 'SELECT steamid,name,avatar,perfect,total FROM scores WHERE steamid IN (' +
      batch.map(() => '?').join(',') + ')';
    const res = await env.DB.prepare(q).bind(...batch).all();
    for (const r of (res.results || [])) known[r.steamid] = r;
  }

  const friends = [];
  for (const id of all) {
    const k = known[id];
    if (!k) continue; // not a Fanfara user (or hasn't registered yet)
    friends.push({ you: id === steamid, name: k.name, avatar: k.avatar, perfect: k.perfect, total: k.total, ready: true });
  }
  friends.sort((a, b) => (b.perfect - a.perfect) || (b.total - a.total));
  friends.forEach((f, i) => { f.rank = i + 1; });

  // friendsPrivate: we couldn't read the friend list (private), so we can only show the user.
  return json({ ok: true, friendsPrivate: ids.length === 0, friends, pending: 0 });
}

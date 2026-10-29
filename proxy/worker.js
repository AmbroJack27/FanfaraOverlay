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
  if (res.status === 200) ctx.waitUntil(cache.put(request, res.clone()));
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
    return json({ ok: true, private: true, games: [] }, 200, 1800);
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

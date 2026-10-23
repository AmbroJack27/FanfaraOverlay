// Fanfara Steam history proxy — Cloudflare Worker.
//
// Purpose: let the Fanfara desktop app rebuild a user's unlocked-achievement history
// WITHOUT shipping a Steam Web API key inside the (public) app. The key lives here as a
// server-side secret (STEAM_KEY). The Worker only relays READ-ONLY public Steam data:
//   - the user's owned games
//   - the user's unlocked achievements per game (with unlock time)
//   - each achievement's global unlock % (rarity)
//
// No personal data is stored. Responses are cached briefly to respect Steam's rate limits.
// The user never enters anything: the app reads their SteamID locally and calls this Worker.
//
// Deploy: see README.md in this folder. Set the key with:  npx wrangler secret put STEAM_KEY

const STEAM = 'https://api.steampowered.com';

export default {
  async fetch(request, env, ctx) {
    const url = new URL(request.url);
    if (request.method !== 'GET') return json({ ok: false, error: 'method' }, 405);

    // Health / root need no parameters — handle them before validating steamid.
    if (url.pathname === '/' || url.pathname === '/health') {
      return json({ ok: true, service: 'fanfara-steam' }, 200);
    }

    const steamid = url.searchParams.get('steamid') || '';
    if (!/^\d{17}$/.test(steamid)) return json({ ok: false, error: 'steamid' }, 400);

    const lang = (url.searchParams.get('l') || 'italian').replace(/[^a-z]/gi, '').slice(0, 20) || 'italian';

    try {
      if (url.pathname === '/owned') {
        return await withCache(request, ctx, () => owned(steamid, env));
      }
      if (url.pathname === '/ach') {
        const appid = url.searchParams.get('appid') || '';
        if (!/^\d{1,8}$/.test(appid)) return json({ ok: false, error: 'appid' }, 400);
        return await withCache(request, ctx, () => ach(steamid, appid, lang, env));
      }
      return json({ ok: false, error: 'not_found' }, 404);
    } catch (e) {
      return json({ ok: false, error: 'upstream' }, 502);
    }
  }
};

function json(obj, status = 200, maxAge = 0) {
  return new Response(JSON.stringify(obj), {
    status,
    headers: {
      'content-type': 'application/json; charset=utf-8',
      'cache-control': maxAge ? ('public, max-age=' + maxAge) : 'no-store'
    }
  });
}

// Edge cache keyed by the full request URL (per user, per game). Only 200s are cached.
async function withCache(request, ctx, producer) {
  const cache = caches.default;
  const hit = await cache.match(request);
  if (hit) return hit;
  const res = await producer();
  if (res.status === 200) ctx.waitUntil(cache.put(request, res.clone()));
  return res;
}

async function owned(steamid, env) {
  const u = `${STEAM}/IPlayerService/GetOwnedGames/v1/?key=${env.STEAM_KEY}`
    + `&steamid=${steamid}&include_appinfo=1&include_played_free_games=1&format=json`;
  const r = await fetch(u);
  const j = await r.json().catch(() => ({}));
  const resp = j && j.response;
  // An empty response object means the profile's game details are private (or there are no games).
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
  // Schema gives each achievement's real icon URL (the colourful unlocked icon shown in Steam).
  const su = `${STEAM}/ISteamUserStats/GetSchemaForGame/v2/?key=${env.STEAM_KEY}&appid=${appid}&l=${lang}&format=json`;

  const [pr, gr, sr] = await Promise.all([fetch(pu), fetch(gu), fetch(su)]);
  const pj = await pr.json().catch(() => ({}));
  const gj = await gr.json().catch(() => ({}));
  const sj = await sr.json().catch(() => ({}));

  const ps = pj && pj.playerstats;
  if (!ps || ps.success === false || !Array.isArray(ps.achievements)) {
    // Game has no achievements, or this title's stats are private.
    return json({ ok: true, appid: Number(appid), achievements: [] }, 200, 3600);
  }

  // Global rarity: apiname -> percent
  const pct = {};
  const garr = gj && gj.achievementpercentages && gj.achievementpercentages.achievements;
  if (Array.isArray(garr)) for (const a of garr) pct[a.name] = Number(a.percent);

  // Icons: apiname -> unlocked icon URL
  const icons = {};
  const sarr = sj && sj.game && sj.game.availableGameStats && sj.game.availableGameStats.achievements;
  if (Array.isArray(sarr)) for (const a of sarr) if (a.name) icons[a.name] = a.icon || '';

  const out = [];
  for (const a of ps.achievements) {
    if (a.achieved !== 1) continue;
    out.push({
      api: a.apiname,
      name: a.name || a.apiname,
      desc: a.description || '',
      unlock: a.unlocktime || 0,
      percent: (a.apiname in pct) ? pct[a.apiname] : null,
      icon: icons[a.apiname] || ''
    });
  }
  return json({ ok: true, appid: Number(appid), game: ps.gameName || '', achievements: out }, 200, 3600);
}

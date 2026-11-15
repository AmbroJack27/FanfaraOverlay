# Posts a new GitHub Release to the Fanfara Discord server (#annunci) through a webhook.
# Run by .github/workflows/build.yml; skipped silently if the DISCORD_RELEASE_WEBHOOK secret is not set.
# Announcements are Italian only: it posts the "Novità" section of the bilingual release notes.
import json, os, re, urllib.request
hook = os.environ.get('DISCORD_RELEASE_WEBHOOK', '').strip()
if not hook:
    print('DISCORD_RELEASE_WEBHOOK not set: skipping the Discord announcement')
    raise SystemExit(0)
tag, repo = os.environ['TAG'], os.environ['REPO']
api = os.environ.get('API', 'https://api.github.com')
ua = {'User-Agent': 'fanfara-release-bot'}
req = urllib.request.Request(f'{api}/repos/{repo}/releases/tags/{tag}',
      headers={**ua, 'Authorization': 'Bearer ' + os.environ['GH_TOKEN'], 'Accept': 'application/vnd.github+json'})
rel = json.load(urllib.request.urlopen(req))
body = (rel.get('body') or '').replace('\r\n', '\n')
# announcements are Italian only: take the "## ... Novità" section of the bilingual notes
m = re.search(r'##[^\n]*Novit[^\n]*\n(.*?)(?=\n##\s|\Z)', body, re.S)
it = (m.group(1) if m else body).strip()
if len(it) > 3500:
    it = it[:3500].rsplit('\n', 1)[0] + '\n…'
desc = (it + '\n\n⬇️ **Scarica**: https://github.com/' + repo + '/releases/latest'
        + '\n🔄 Se hai già Fanfara, l’aggiornamento arriva da solo.')
payload = {'username': 'Fanfara', 'allowed_mentions': {'parse': []},
           'embeds': [{'title': f'🎉 Fanfara {tag} è disponibile!', 'url': rel.get('html_url'),
                       'description': desc, 'color': 0xF5C242}]}
urllib.request.urlopen(urllib.request.Request(hook, data=json.dumps(payload).encode('utf-8'),
                       headers={**ua, 'Content-Type': 'application/json'}))
print('Announced', tag, 'on Discord')

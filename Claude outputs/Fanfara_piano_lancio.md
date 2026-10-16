# Piano di lancio Fanfara 🎺

**Idea guida:** il prodotto è gratis e visivo, quindi si condivide da solo. Due regole d'oro:
1. Concentra il lancio in **pochi giorni** (una "settimana di lancio") invece di post sparsi nel tempo.
2. L'asset che fa la differenza è una **clip breve** (lo sblocco epico): 15–30s, verticale per Shorts/TikTok, orizzontale per Reddit.

---

## ✅ Prima di lanciare (prep, ~1 ora)
- [ ] README di GitHub con una **GIF animata in cima** (posso fartela dai video)
- [ ] **Topics** sul repo GitHub (steam, achievements, overlay, windows, csharp, gaming…)
- [ ] **Social preview image** su GitHub (Settings → Social preview) — così il link mostra un'anteprima bella
- [ ] Video YouTube pubblicati (IT + EN) con le copertine nuove
- [ ] **Clip breve** pronta (posso fartela)
- [ ] Release v0.1.x pubblicata con l'installer allegato

---

## 📅 Settimana di lancio (esempio)
Pubblica di **martedì/mercoledì mattina (ora USA, ~15:00–17:00 in Italia)**: è quando Reddit e Product Hunt girano di più.

- **Giorno 1** — Reddit "amichevoli coi creatori": r/coolgithubprojects, r/SideProject. + Product Hunt.
- **Giorno 2** — Il subreddit grosso di gaming: r/pcgaming **oppure** r/Steam (uno solo, quello più adatto).
- **Giorno 3** — r/opensource + scheda su AlternativeTo.
- **Giorno 4** — Short/TikTok con la clip verticale + post nel tuo gruppo Steam e in 1–2 server Discord di gaming.
- **Settimana 2** — Outreach via email/DM a 5–10 YouTuber/blog che coprono tool PC gratuiti.

Regola anti-ban: **un subreddit alla volta**, leggi le regole della sidebar, scegli il flair giusto, e **rispondi a tutti i commenti** nelle prime ore (conta più del post stesso).

---

## 📝 Contenuti pronti (copia-incolla)

### Reddit — gaming (EN)
**Titolo:** I got tired of Steam's boring achievement popup, so I made a free app that turns every unlock into a celebration
**Corpo:**
Hi everyone! I built **Fanfara**, a free and open-source Windows app that replaces Steam's forgettable achievement popup with a celebratory, animated notification — with an orchestral sound and a different look depending on the rarity (Common → Legendary).

It works with any Steam game, runs quietly in the tray, has lots of styles, and even shows up over games using Frame Generation / exclusive fullscreen. 100% free, no ads, open source.

Would love your feedback and ideas!
GitHub: https://github.com/AmbroJack27/FanfaraOverlay
(Upload the short clip directly to Reddit; put the GitHub link in the post body or first comment.)

### Reddit — open source (EN)
**Titolo:** [Show] Fanfara — a C#/WPF app that makes Steam achievement unlocks celebratory (free, MIT)
**Corpo:**
I made Fanfara, an open-source Windows overlay (C# / WPF + WebView2) that turns Steam achievement unlocks into animated, rarity-based notifications with sound. It detects the achievement's global rarity and picks an emblem + orchestral fanfare to match.
Happy to chat about the architecture (separate process per game, Steamworks, click-through topmost overlay). Feedback and PRs welcome!
Repo: https://github.com/AmbroJack27/FanfaraOverlay

### X / Twitter — thread (EN)
1/ I was tired of Steam's forgettable achievement popup. So I built **Fanfara**: every unlock becomes a celebration — animated, orchestral sound, and a different look per rarity. Free & open source. 🧵👇 [clip]
2/ Common → Legendary: each rarity gets its own emblem and fanfare. [clip/immagini]
3/ Works with any Steam game, lives in the tray, and even shows over games using Frame Generation / exclusive fullscreen.
4/ 100% free, no ads, open source. Download + star ⭐ https://github.com/AmbroJack27/FanfaraOverlay — feedback very welcome!
(Hashtag: #Steam #PCGaming #indiedev #opensource)

### Product Hunt (EN)
**Tagline:** Turn every Steam achievement unlock into an epic moment
**Descrizione:** Fanfara replaces Steam's bland achievement popup with celebratory, animated notifications — orchestral sounds and a different look based on rarity (Common → Legendary). Works with any game, runs in the tray, even over Frame Generation / exclusive fullscreen. Free & open source.
**Primo commento (maker):** Solo dev here 👋 I built this because unlocking a rare achievement deserves more than a tiny grey toast. It's completely free and open source — I'd love your feedback on styles and sounds!

### AlternativeTo (EN)
- **Name:** Fanfara
- **Short description:** Free, open-source Windows app that replaces Steam's achievement popup with celebratory, rarity-based animated notifications and sounds.
- **License:** Open Source · **Platform:** Windows · **Tags:** steam, achievements, overlay, notifications
- **Link:** https://github.com/AmbroJack27/FanfaraOverlay

### Discord / Gruppo Steam (IT)
Ho appena rilasciato **Fanfara**, un'app gratis e open source che trasforma il noioso popup degli obiettivi di Steam in una notifica animata e celebrativa, con suono orchestrale diverso per rarità (dal Comune al Leggendario). Funziona con qualsiasi gioco, sta nella tray, e si vede anche in game con Frame Generation. Se la provate mi fate un favore enorme con un feedback! ⭐ https://github.com/AmbroJack27/FanfaraOverlay

### Discord / Steam group (EN)
Just released **Fanfara** — a free & open-source app that turns Steam's boring achievement popup into a celebratory animated notification, with a different orchestral sound per rarity (Common → Legendary). Works with any game, lives in the tray, even shows over Frame Generation. Would love feedback! ⭐ https://github.com/AmbroJack27/FanfaraOverlay

### Email / DM a YouTuber & blog (EN)
**Oggetto:** A free open-source tool that makes Steam achievements feel epic
Hi <nome>,
I'm a solo dev and I made **Fanfara**, a free & open-source Windows app that replaces Steam's bland achievement popup with celebratory, animated, rarity-based notifications (with orchestral sound). I thought it could be a fun, quick feature for your PC-gaming audience.
30-second demo: <LINK VIDEO YT>
Repo: https://github.com/AmbroJack27/FanfaraOverlay
No strings attached — it's free and open source. Happy to answer anything. Thanks for your time!
<nome>

### Email / DM (IT)
**Oggetto:** Un tool gratis e open source che rende epici gli obiettivi di Steam
Ciao <nome>,
sono uno sviluppatore indipendente e ho creato **Fanfara**, un'app gratuita e open source per Windows che sostituisce il banale popup degli obiettivi di Steam con notifiche animate e celebrative, diverse per rarità (con suono orchestrale). Ho pensato potesse essere un contenuto veloce e simpatico per il tuo pubblico di giocatori PC.
Demo di 30 secondi: <LINK VIDEO YT>
Repo: https://github.com/AmbroJack27/FanfaraOverlay
È tutto gratis e open source, nessun obbligo. Resto a disposizione, grazie!
<nome>

---

## 📈 Dopo il lancio
- Rispondi a **ogni** commento/issue nelle prime 48h.
- Se un post va bene, **non ripubblicarlo** altrove subito: aspetta qualche giorno.
- Tieni d'occhio le ⭐ su GitHub e gli iscritti YouTube: dove arrivano più persone, raddoppia lì.
- Ogni nuova versione = un mini-annuncio (changelog con GIF).

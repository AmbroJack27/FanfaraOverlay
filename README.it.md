# 🎉 Fanfara

[🇬🇧 Read in English](README.md) · **🇮🇹 Italiano**

**Rendi lo sblocco degli achievement di Steam spettacolare come su Xbox e PlayStation.**

Su Steam, quando sblocchi un obiettivo, compare una notifica piccola e anonima in basso a
destra. Fanfara la sostituisce con un'animazione festante, con **suono e stile che cambiano
in base alla rarità** dell'obiettivo: più è raro (pochi giocatori al mondo l'hanno ottenuto),
più la notifica è epica.

È un overlay **leggero e nativo per Windows**: non pesa sulle prestazioni e non richiede ore
di configurazione. Lo avvii, giochi, e pensa a tutto lui.

---

## ⬇️ Scaricare e usare (per tutti)

1. Vai nella sezione **[Releases](../../releases)** qui su GitHub.
2. Scarica il file **`Fanfara.zip`** dell'ultima versione.
3. Fai clic destro sullo zip → **Estrai tutto**.
4. Apri la cartella estratta e fai doppio clic su **`Fanfara.exe`**.

Non serve installare nulla: niente .NET, niente terminale. È tutto incluso nel pacchetto.

> ⚠️ **La prima volta** Windows potrebbe mostrare l'avviso blu "Windows ha protetto il tuo PC".
> È normale per i programmi gratuiti non firmati con un certificato a pagamento.
> Clicca su **"Ulteriori informazioni"** → **"Esegui comunque"**.

Al primo avvio si apre la finestra delle **impostazioni**: puoi scegliere lo stile della
notifica, il suono, la posizione sullo schermo e la durata, e provarli subito con il tasto
**Prova**. Dopo averla chiusa, Fanfara resta attivo con un'icona vicino all'orologio (in basso
a destra): clicca lì per riaprire le impostazioni o per uscire.

Poi avvia Steam, gioca, e al primo achievement sbloccato comparirà la fanfara. 🎮

### Consiglio: spegni la notifica di Steam
Per non vedere doppioni, puoi disattivare quella standard di Steam:
**Steam → Impostazioni → In gioco** (o **Notifiche**) → togli la notifica di sblocco achievement.

---

## ✨ Cosa include

- **15 stili di notifica** — Console, Minimal, Arcade, Neon, Loot, Terminale, Pixel, Nastro,
  scudi fantasy in stile League of Legends, più le versioni 3D di ognuno.
- **28 suoni** — da melodici e cinematografici a 8-bit, epici orchestrali, fanfare da gamer.
- **5 livelli di rarità** che cambiano colore, intensità ed effetti: Comune, Non comune, Raro,
  Epico, Leggendario.
- **7 posizioni** sullo schermo a scelta.

---

## 🛠️ Per sviluppatori (compilare da sé)

Serve solo il **.NET 8 SDK** (gratuito): https://dotnet.microsoft.com/download

Nella cartella `src/`:

```bat
dotnet run
```

per avviarlo in sviluppo, oppure per produrre il pacchetto distribuibile:

```bat
dotnet publish -c Release
```

L'app finita esce in `src/bin/Release/.../publish/`. È *self-contained* (include il runtime
.NET), quindi la cartella è autonoma e trasferibile su qualsiasi PC Windows.

> L'app va eseguita **sul PC dove gira Steam**. Fanfara si aggancia al gioco in esecuzione
> leggendo l'AppID dal registro e usando la Steamworks API come quel gioco (nessuna API key).

### Pubblicare una nuova versione
Nella repo è incluso `.github/workflows/build.yml`. Quando pubblichi una **Release** con un
**tag** che inizia per `v` (es. `v0.1.0`), GitHub compila l'app sui propri server Windows e
allega da solo `Fanfara.zip` alla Release. Nessuna compilazione manuale.

---

## ⚙️ Come funziona (in breve)

- `SteamWatcher.cs` — legge l'AppID del gioco in esecuzione dal registro
  (`HKCU\Software\Valve\Steam\RunningAppID`), inizializza la Steamworks API come quel gioco, e
  ogni secondo controlla se è stato sbloccato un nuovo achievement. Per ognuno ricava la
  percentuale mondiale (rarità) e lancia un evento.
- `OverlayWindow` — una finestra trasparente, sempre in primo piano e *click-through* (non
  intercetta i clic del mouse) che copre lo schermo e contiene un WebView2.
- `web/notify.html` — la grafica delle notifiche: riceve i dati dal guscio C# e disegna lo
  sblocco nello stile e suono scelti, con la rarità che decide colori ed effetti.
- `web/settings.html` — la finestra delle impostazioni.
- `config.json` — le preferenze salvate, accanto all'exe.

### Note oneste
- L'overlay compare sopra i giochi in **finestra senza bordi** (lo standard di oggi). Il
  *fullscreen esclusivo* vero è l'unico caso in cui potrebbe non comparire — stesso limite di
  tutti gli overlay simili.
- I suoni sono per ora **sintetizzati dal vivo**: definiscono il carattere e si possono
  sostituire con campioni audio veri più avanti.

## Licenza
MIT — vedi `LICENSE`. Libero di usarlo, modificarlo e condividerlo.

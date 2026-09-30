# Fanfara

Un overlay leggero per Windows che rende lo sblocco degli achievement di Steam più bello e
festante — animazione, suono e stile che cambiano in base alla **rarità** dell'obiettivo
(percentuale mondiale di giocatori che l'hanno ottenuto).

Il guscio dell'app è nativo (C# / WPF), mentre le notifiche sono disegnate con la stessa
grafica del prototipo (HTML/CSS/JS) mostrata dentro **WebView2**, il motore web già presente
in Windows 10/11.

> **Stato: v0** — prima versione da compilare e collaudare sul PC da gaming con Steam aperto.
> I punti dell'aggancio a Steam da verificare/ritoccare sono marcati con `TODO(steam)` in
> `src/SteamWatcher.cs`.

---

## Per chi lo scarica (utente finale)

Scarica l'ultimo `Fanfara.exe` dalla pagina **Releases** e fai doppio clic. Non serve
installare nulla: l'exe è *self-contained* (include il runtime .NET) e WebView2 è già in Windows.
Avvia Steam, gioca, e allo sblocco di un achievement comparirà la notifica.

Le preferenze (stile, suono, durata) stanno in `config.json` accanto all'exe.

---

## Per sviluppare / compilare (PC da gaming, una volta)

Serve una sola cosa, gratuita:

- **.NET 8 SDK** → https://dotnet.microsoft.com/download (oppure Visual Studio 2022 Community).

Poi, nella cartella `src/`:

```bat
dotnet run
```

per avviarlo in sviluppo, oppure per produrre l'exe da distribuire:

```bat
dotnet publish -c Release
```

L'exe finito esce in
`src/bin/Release/net8.0-windows/win-x64/publish/Fanfara.exe`.
Portalo dove vuoi: è autonomo.

### Collaudo
L'app va **eseguita sul PC dove gira Steam**. Avvia un gioco con achievement, sbloccane uno
(molti giochi hanno un achievement iniziale facile) e verifica che la notifica compaia.
Se non appare, si guarda insieme `SteamWatcher.cs`: i nomi esatti di alcune funzioni Steamworks
possono cambiare tra versioni della libreria (righe `TODO(steam)`).

---

## Distribuire su GitHub (l'exe lo costruisce GitHub da solo)

Nella repo è incluso `.github/workflows/build.yml`. Quando crei un **tag** che inizia per `v`
(es. `v0.1.0`) e lo pubblichi, GitHub compila l'exe sui suoi server Windows e lo allega da solo
a una Release. Così non devi compilare a mano per pubblicare.

```bat
git tag v0.1.0
git push origin v0.1.0
```

(Puoi anche lanciare il workflow a mano dalla scheda **Actions → Build → Run workflow**.)

---

## Come funziona (in breve)

- `SteamWatcher.cs` — legge dal registro l'AppID del gioco in esecuzione
  (`HKCU\Software\Valve\Steam\RunningAppID`), inizializza la Steamworks API *come quel gioco*
  (niente API key), e ogni secondo controlla se è stato sbloccato un nuovo achievement.
  Per ognuno ricava la **percentuale mondiale** (rarità) e lancia un evento.
- `OverlayWindow.xaml(.cs)` — una finestra trasparente, sempre in primo piano e
  *click-through* (non intercetta i clic), che copre lo schermo e contiene un WebView2.
- `web/notify.html` — la grafica delle notifiche: riceve `window.fanfara.show({...})` dal
  guscio C# e disegna la notifica nello stile/suono scelti, con la rarità che decide colore,
  scintille e intensità.
- `config.json` — preferenze dell'utente.

### Note tecniche oneste
- L'overlay disegna sopra i giochi in **finestra senza bordi** (lo standard oggi). Il
  *fullscreen esclusivo* vero è l'unico caso in cui un overlay così può non comparire — stesso
  limite dei tool simili.
- I suoni in `notify.html` sono ancora **sintetizzati** dal vivo: servono a definire il
  carattere. Si possono sostituire con campioni audio veri più avanti.
- In v0 è incluso un sottoinsieme rappresentativo di stili (Arcade, Neon, Loot, Terminale).
  Gli altri stili del prototipo si incollano in `notify.html` con la stessa struttura.

## Licenza
MIT — vedi `LICENSE`.

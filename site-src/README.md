# Sito vetrina di Pelago

La cartella [`site/`](../site) è il sito pubblicato (GitHub Pages, vedi `.github/workflows/pages.yml`). Si genera da qui:

| Cosa | Dove |
|---|---|
| La pagina principale e la pagina «Provalo», con segnaposto `{{chiave}}` | `site-src/template.html`, `site-src/provalo.html` |
| Le parti comuni (testa, intestazione con le bandiere, piede) | `site-src/partials/*.html` |
| I testi, uno per lingua (italiano, inglese, francese, tedesco, spagnolo): **le stesse chiavi in tutti** | `site-src/strings/*.json` |
| Lo stile (tema chiaro e scuro automatico) | `site-src/style.css` |
| Il generatore e i controlli | `tools/build-site.ps1` |
| La demo nel browser (il programma vero, in una pagina web) | `Web/` + `tools/build-demo.ps1` |

Per rigenerare le pagine dopo una modifica (da PowerShell, nella cartella del progetto):

```powershell
.\tools\build-site.ps1
```

Lo script scrive `site/index.html` (italiano), `site/<lingua>/index.html` e le pagine «Provalo» (`site/provalo/`, `site/<lingua>/provalo/`), copia stile, carattere e bandiere, e controlla: segnaposto rimasti, chiavi mancanti in una lingua, file e collegamenti locali inesistenti, risorse caricate da altri siti, immagini senza testo alternativo. Esce con errore se trova qualcosa.

## La demo nel browser

È **lo stesso programma** (le stesse pagine, gli stessi calcoli, gli stessi grafici) compilato per il browser con Blazor WebAssembly, con il conto di esempio a dati inventati. Non è un secondo programma da mantenere: `tools/build-demo.ps1` copia i sorgenti di Pelago in `Web/_build/` (cartella generata), toglie i pezzi che esistono solo in Windows, aggiunge i file di `Web/` e pubblica il risultato in `site/provalo/demo/`.

- `Web/Program.cs` avvia la demo e prepara il conto di esempio in un disco virtuale in memoria (lingua e tema dall'indirizzo: `?lang=de&theme=dark`);
- `Web/NativeDialogs.cs` sostituisce le finestre di apri/salva file di Windows;
- `Web/index.web.html` è la pagina ospite, con una **politica di sicurezza del browser (CSP)** che vieta ogni collegamento fuori dal sito: la demo non può inviare dati a nessuno, nemmeno per errore;
- dove il programma userebbe internet, file o cartelle dell'utente, il codice condiviso controlla `AppState.IsWeb` (sempre falso in Windows) e nasconde la funzione.

`site/provalo/demo/` **non si committa** (13 MB che cambierebbero a ogni modifica): lo costruisce la pubblicazione automatica, e in locale basta lanciare `.\tools\build-demo.ps1` (la prima volta serve internet per i pacchetti NuGet). Dopo aver cambiato testi dell'app o interfaccia, rilanciarlo.

## Anteprima prima di pubblicare

```powershell
.\tools\build-demo.ps1        # una volta, e ogni volta che cambia il programma (costruisce la demo)
.\tools\build-site.ps1        # dopo ogni modifica a testi o stile
.\tools\preview-site.ps1
```

`preview-site.ps1` avvia un piccolo server che risponde solo a questo computer, mostra `site/` allo stesso percorso di GitHub Pages (`http://localhost:8080/pelago/`) e apre il browser. Non pubblica nulla. Ctrl+C per fermarlo; dopo una modifica basta rigenerare e ricaricare la pagina.

## Principi

- **Nessuna risorsa da altri siti**: né caratteri, né script, né statistiche, né cookie. È ciò che la pagina dichiara nel piè di pagina: se un giorno cambia, va cambiato anche quel testo. Nella demo lo garantisce il browser (CSP `connect-src 'self'`).
- **Nessun JavaScript**, tranne un breve script nelle pagine italiane che, alla prima visita senza una scelta esplicita, porta alla lingua del browser (non salva nulla), e naturalmente la demo.
- **Collegamenti relativi**: il sito funziona sia sotto `…github.io/pelago/` sia su un dominio proprio. Per un dominio proprio: `.\tools\build-site.ps1 -BaseUrl https://esempio.it/` e `.\tools\build-demo.ps1 -BasePath /provalo/demo/` (servono per `canonical`, `hreflang`, sitemap, anteprime social e per la pagina ospite della demo).
- «Trade Republic» compare solo come testo, mai con logo o colori, e sempre con «non affiliata».

## Schermate, video, anteprime social

Stanno già in `site/assets/` (si committano). Si producono sul PC di chi cura il sito, con gli strumenti in `Presentazione/strumenti/` (cartella locale, non pubblicata), sempre sul **conto di esempio con dati inventati**:

1. `capture.ps1 -Lang <lingua> -Only ...` fotografa le pagine dell'app;
2. `make-site-assets.ps1` le converte in WebP (800 e 1600 px), prepara le icone e le anteprime social (`og-<lingua>.jpg`);
3. `record-clip.ps1` registra il filmato leggero senza audio (`pelago-<lingua>.mp4`, meno di 1 MB).

Se cambiano i testi del sito, rilanciare `make-site-assets.ps1` aggiorna anche le anteprime social (che contengono il titolo).

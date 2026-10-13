# Sito vetrina di Pelago

La cartella [`site/`](../site) è il sito pubblicato (GitHub Pages, vedi `.github/workflows/pages.yml`). Si genera da qui:

| Cosa | Dove |
|---|---|
| La pagina, con segnaposto `{{chiave}}` | `site-src/template.html` |
| I testi, uno per lingua (italiano, inglese, francese, tedesco, spagnolo): **le stesse chiavi in tutti** | `site-src/strings/*.json` |
| Lo stile (tema chiaro e scuro automatico) | `site-src/style.css` |
| Il generatore e i controlli | `tools/build-site.ps1` |

Per rigenerare le pagine dopo una modifica (da PowerShell, nella cartella del progetto):

```powershell
.\tools\build-site.ps1
```

Lo script scrive `site/index.html` (italiano) e `site/<lingua>/index.html`, copia stile, carattere e bandiere, e controlla: segnaposto rimasti, chiavi mancanti in una lingua, file e collegamenti locali inesistenti, risorse caricate da altri siti, immagini senza testo alternativo. Esce con errore se trova qualcosa.

## Principi

- **Nessuna risorsa da altri siti**: né caratteri, né script, né statistiche, né cookie. È ciò che la pagina dichiara nel piè di pagina: se un giorno cambia, va cambiato anche quel testo.
- **Nessun JavaScript**, tranne un breve script nella pagina italiana che, alla prima visita senza una scelta esplicita, porta alla lingua del browser. Non salva nulla.
- **Collegamenti relativi**: il sito funziona sia sotto `…github.io/pelago/` sia su un dominio proprio. Per un dominio proprio: `.\tools\build-site.ps1 -BaseUrl https://esempio.it/` (serve per `canonical`, `hreflang`, sitemap e anteprime social).
- «Trade Republic» compare solo come testo, mai con logo o colori, e sempre con «non affiliata».

## Schermate, video, anteprime social

Stanno già in `site/assets/` (si committano). Si producono sul PC di chi cura il sito, con gli strumenti in `Presentazione/strumenti/` (cartella locale, non pubblicata), sempre sul **conto di esempio con dati inventati**:

1. `capture.ps1 -Lang <lingua> -Only ...` fotografa le pagine dell'app;
2. `make-site-assets.ps1` le converte in WebP (800 e 1600 px), prepara le icone e le anteprime social (`og-<lingua>.jpg`);
3. `record-clip.ps1` registra il filmato leggero senza audio (`pelago-<lingua>.mp4`, meno di 1 MB).

Se cambiano i testi del sito, rilanciare `make-site-assets.ps1` aggiorna anche le anteprime social (che contengono il titolo).

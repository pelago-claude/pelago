# Sicurezza e dati / Security and data

## Che cosa tratta Pelago

Pelago legge un file CSV **sul tuo PC** e non invia il file né importi o quantità a nessuno: per i prezzi chiede a servizi pubblici solo codici di titoli e date (elenco completo nel [README](README.md)). Non chiede login né password e non ha accesso al conto.

## Segnalare un problema di sicurezza

Se pensi di aver trovato una vulnerabilità (per esempio un modo per far uscire dati dal PC, o per eseguire codice aprendo un CSV costruito ad arte), **non aprire una Issue pubblica**:

- usa *Security → Report a vulnerability* in questo repository (segnalazione privata di GitHub);
- oppure scrivi a **pelago_claude@gmail.com**.

Descrivi i passi per riprodurre il problema **senza allegare dati finanziari tuoi**. Si tratta di un progetto personale: risponderò appena possibile, senza tempi garantiti.

## Versioni supportate

Solo l'ultima versione pubblicata nelle *Releases*.

## Verificare l'eseguibile

Ogni release riporta l'impronta SHA-256 di `pelago.exe`. In PowerShell: `Get-FileHash .\pelago.exe -Algorithm SHA256`. L'eseguibile non è firmato digitalmente.

---

**English.** Pelago reads a CSV file locally and sends nothing but instrument codes and dates to public price services (see the README). To report a vulnerability, use *Security → Report a vulnerability* on this repository rather than a public issue, and never attach real financial data. Only the latest release is supported. Each release lists the SHA-256 of `pelago.exe`; the executable is not code-signed.

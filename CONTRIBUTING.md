# Come contribuire / Contributing

Grazie dell'interesse! Pelago è un progetto personale e senza scopo di lucro: ogni segnalazione e ogni idea aiutano a migliorarlo. *(English summary at the bottom.)*

## ⚠️ Prima di tutto: i tuoi dati

**Non allegare mai il tuo CSV vero, né schermate con i tuoi importi, nomi di titoli o codici ISIN del tuo conto.** Sono dati finanziari personali e un'*Issue* di GitHub è pubblica per chiunque.

Per mostrare un problema:
- descrivilo a parole (che pagina, che cosa ti aspettavi, che cosa è successo);
- se dipende dal formato del file, copia **una sola riga** del CSV dopo aver sostituito importi, ISIN, nomi, IBAN e id di transazione con valori inventati;
- oppure riproducilo con [samples/esempio-operazioni.csv](samples/esempio-operazioni.csv).

Se per sbaglio hai pubblicato dati tuoi, modifica o cancella il commento subito e scrivimelo.

## Che cosa è utile

- **Feedback d'uso**: che cosa ti serve di più, che cosa non si capisce, che cosa manca, che versione di Windows usi.
- **Segnalazioni di errori**: un numero che non torna (indica la pagina e, se puoi, un esempio inventato), un testo tradotto male, un formato del CSV che l'app non legge.
- **Traduzioni**: i testi sorgente sono in italiano; le traduzioni sono in `i18n/` (en, fr, de, es). Vedi `i18n/GLOSSARY.md`.
- **Codice**: apri prima una *Issue* per discutere l'idea, poi una *Pull Request* piccola e mirata.

## Per chi sviluppa

- Servono Windows e l'SDK .NET 8. Compila e prova: `dotnet test Tests/Pelago.Tests.csproj`.
- Prima di una PR i test devono passare e i nuovi comportamenti devono avere un test (i test usano **solo dati inventati**: nei codici ISIN non mettere titoli veri del tuo conto).
- Ogni testo visibile passa da `T("…")` (testo italiano). Se ne aggiungi uno, aggiungi anche le quattro traduzioni, o segnalalo nella PR: `LocalizationTests` fallisce se ne manca una.
- Ogni versione ha una voce nuova in `Services/ChangeLog.cs` e `<Version>` allineata in `Pelago.csproj`.
- Il codice di chi contribuisce è distribuito con la stessa licenza del progetto, **GPL-3.0**.

## Cosa non verrà accettato

- Qualunque cosa che faccia uscire dal PC i dati del conto (importi, quantità, il CSV).
- Funzioni che richiedono il login o le credenziali del broker.
- Lettura di siti che ne vietano espressamente l'uso automatico, se non resta **spenta di default** e con un avviso chiaro.

---

**English summary.** Thanks! Please **never attach your real CSV or screenshots showing your real amounts/ISINs** to an issue (it's public): describe the problem, use made-up values for a single CSV line, or reproduce it with `samples/esempio-operazioni.csv`. Useful: usage feedback, bug reports, translations (`i18n/`), small focused PRs (open an issue first). Tests must pass (`dotnet test Tests/Pelago.Tests.csproj`) and use invented data only. Contributions are licensed under GPL-3.0.

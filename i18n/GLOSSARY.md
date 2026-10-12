# Pelago — glossario e regole di traduzione

Pelago è un'app Windows personale per tenere sotto controllo il proprio conto titoli (broker online): panoramica, rendimenti, costi e tasse,
cedole dei titoli di Stato, cambi, aiuto integrato. I testi sorgente sono in **italiano**; vanno tradotti in **inglese (en), francese (fr),
tedesco (de), spagnolo (es)**. Tono: chiaro, amichevole, semplice, mai burocratico (l'app si rivolge a un risparmiatore, non a un professionista).
Si dà del «tu» (en: you; fr: tu; de: du; es: tú).

## Regole tecniche (OBBLIGATORIE)
1. **Segnaposto**: `{0}`, `{1:dd/MM/yyyy}`, `{2:N2}`, `{total}`, `{cash}`… restano IDENTICI (stesso nome/numero e stesso formato), in qualunque ordine nella frase.
   Non tradurre né modificare ciò che sta dentro le graffe.
2. **HTML**: `<b>…</b>`, `<a href="btp">…</a>` ecc. restano identici (stessi tag e attributi): si traduce solo il testo in mezzo.
3. **Spazi e punteggiatura ai bordi**: molte chiavi sono frammenti (esempi: `" su {0}"`, `". Può essere un cambio di valuta."`, `"; e altri {0}"`, `" · chiusa"`).
   La traduzione deve cominciare e finire con gli STESSI spazi/segni (spazio iniziale, punto iniziale, ecc.) perché il frammento viene incollato ad altro testo.
4. **Parole chiave per la ricerca** (stringhe di parole minuscole senza punteggiatura, esempio `"posizioni peso concentrazione"`): traduci in un elenco equivalente di parole
   minuscole separate da spazio nella lingua di arrivo (senza punteggiatura), con sinonimi utili alla ricerca.
5. **Non tradurre**: nomi propri e sigle (BTP, TWR, XIRR, ISIN, CSV, PDF, Excel, Bund, OAT, ETF, Yahoo Finance, CoinGecko, Frankfurter, Borsa Italiana, TreasuryDirect, Bundesbank, Claude, Anthropic, David), nomi di file (btp.json…),
   scorciatoie da tastiera (Ctrl K, F1, F2, Esc), percorsi, esempi di colonne del file CSV in maiuscolo (TAX, TAX_OPTIMIZATION, BUY, SELL), simboli (€, %, ▲, ▼, →).
6. Le virgolette italiane «…» possono diventare le virgolette tipiche della lingua (“…” en, « … » fr, „…“ de, «…» es).
7. Formato numeri/date: non scrivere numeri formattati a mano (li formatta l'app). Il separatore «·» e il trattino lungo «—» si mantengono.
8. Una chiave senza nulla da tradurre (esempio solo sigle) si ripete identica.
9. Mantieni la lunghezza ragionevole: i testi finiscono in pulsanti, tabelle e menu stretti (tedesco: evita parole composte inutilmente lunghe nei titoli brevi).

## Glossario (usa SEMPRE questi termini)
| Italiano | en | fr | de | es |
|---|---|---|---|---|
| Panoramica | Overview | Vue d'ensemble | Übersicht | Resumen |
| Andamento giornaliero | Daily trend | Évolution quotidienne | Tagesverlauf | Evolución diaria |
| Rendimenti | Returns | Rendements | Renditen | Rendimientos |
| Grafici | Charts | Graphiques | Diagramme | Gráficos |
| Composizione a una data | Composition on a date | Composition à une date | Zusammensetzung an einem Datum | Composición en una fecha |
| Entrate / Uscite | Deposits / Withdrawals | Entrées / Sorties | Ein- / Auszahlungen | Entradas / Salidas |
| Interessi | Interest | Intérêts | Zinsen | Intereses |
| Titoli (azioni, fondi, derivati) | Securities (stocks, funds, derivatives) | Titres (actions, fonds, dérivés) | Wertpapiere (Aktien, Fonds, Derivate) | Valores (acciones, fondos, derivados) |
| Azioni / Fondi / Derivati / Crypto | Stocks / Funds / Derivatives / Crypto | Actions / Fonds / Dérivés / Crypto | Aktien / Fonds / Derivate / Krypto | Acciones / Fondos / Derivados / Cripto |
| Obbligazioni | Bonds | Obligations | Anleihen | Bonos |
| Titoli di Stato | Government bonds | Titres d'État | Staatsanleihen | Deuda pública |
| Plusvalenze / Plus/minus | Capital gains / Gain/loss | Plus-values / Plus/moins-values | Kursgewinne / Gewinn/Verlust | Plusvalías / Ganancia/pérdida |
| Plus/minus latente | Unrealized gain/loss | Plus/moins-value latente | Nicht realisierter Gewinn/Verlust | Ganancia/pérdida latente |
| realizzate | realized | réalisées | realisiert | realizadas |
| Costi e tasse | Costs and taxes | Coûts et impôts | Kosten und Steuern | Costes e impuestos |
| Commissioni | Fees | Frais | Gebühren | Comisiones |
| Imposte / tasse | Taxes | Impôts | Steuern | Impuestos |
| Imposta di bollo | Stamp duty | Droit de timbre | Stempelsteuer | Impuesto de timbre |
| Cedole e scadenze | Coupons and maturities | Coupons et échéances | Kupons und Fälligkeiten | Cupones y vencimientos |
| Cedola / cedole | Coupon / coupons | Coupon / coupons | Kupon / Kupons | Cupón / cupones |
| Scadenza | Maturity | Échéance | Fälligkeit | Vencimiento |
| Rimborso | Redemption | Remboursement | Rückzahlung | Reembolso |
| Rateo | Accrued interest | Intérêts courus | Stückzinsen | Cupón corrido |
| Corso secco / prezzo secco | Clean price | Prix pied de coupon | Kurs ohne Stückzinsen | Precio limpio |
| Prezzo tel quel | Dirty price (tel quel) | Prix coupon couru (tel quel) | Kurs inkl. Stückzinsen (tel quel) | Precio sucio (tel quel) |
| Controvalore | Market value | Contre-valeur | Gegenwert | Contravalor |
| Taglio (minimo) | Lot (minimum denomination) | Coupure (minimale) | Stückelung (Mindestbetrag) | Lote (importe mínimo) |
| Valore nominale | Face value | Valeur nominale | Nennwert | Valor nominal |
| Rendimento effettivo a scadenza | Yield to maturity | Rendement à l'échéance | Rendite bis zur Fälligkeit | Rentabilidad al vencimiento |
| Surplus a scadenza | Surplus at maturity (pull-to-par gain) | Surplus à l'échéance | Überschuss bei Fälligkeit | Excedente al vencimiento |
| lordo / netto | gross / net | brut / net | brutto / netto | bruto / neto |
| Versamenti netti | Net deposits | Versements nets | Netto-Einzahlungen | Aportaciones netas |
| Totale conto / Totale | Account total / Total | Total du compte / Total | Kontostand gesamt / Summe | Total de la cuenta / Total |
| Disponibilità / Liquidità / Saldo disponibile | Cash / Available balance | Liquidités / Solde disponible | Guthaben / Verfügbarer Saldo | Efectivo / Saldo disponible |
| Quotazioni | Quotes (prices) | Cours | Kurse | Cotizaciones |
| Valute | Currencies | Devises | Währungen | Divisas |
| cambio | exchange rate | taux de change | Wechselkurs | tipo de cambio |
| Controlli | Checks | Contrôles | Prüfungen | Controles |
| Storia | History | Historique | Verlauf | Historia |
| Dati e file | Data and files | Données et fichiers | Daten und Dateien | Datos y archivos |
| Modalità assistita | Assistant mode | Mode assisté | Hilfemodus | Modo asistente |
| Tema chiaro / scuro | Light / Dark theme | Thème clair / sombre | Helles / Dunkles Design | Tema claro / oscuro |
| Obiettivo | Goal | Objectif | Ziel | Objetivo |
| Concentrazione / Peso | Concentration / Weight | Concentration / Poids | Konzentration / Gewichtung | Concentración / Peso |
| Spread sui Bund | Spread over Bunds | Écart avec les Bunds | Spread zu Bundesanleihen | Diferencial con el Bund |
| punti base (pb) | basis points (bp) | points de base (pb) | Basispunkte (BP) | puntos básicos (pb) |
| Curva dei rendimenti | Yield curve | Courbe des taux | Zinsstrukturkurve | Curva de tipos |
| Rendimento ponderato nel tempo (TWR) | Time-weighted return (TWR) | Rendement pondéré par le temps (TWR) | Zeitgewichtete Rendite (TWR) | Rentabilidad ponderada por tiempo (TWR) |
| Rendimento annuo (XIRR) | Annualized return (XIRR) | Rendement annualisé (XIRR) | Annualisierte Rendite (XIRR) | Rentabilidad anualizada (XIRR) |
| posizione aperta / chiusa | open / closed position | position ouverte / clôturée | offene / geschlossene Position | posición abierta / cerrada |
| Esporta | Export | Exporter | Exportieren | Exportar |
| Aggiorna | Refresh / Update | Actualiser | Aktualisieren | Actualizar |
| Backup | Backup | Sauvegarde | Sicherung | Copia de seguridad |
| Quotazione / prezzo | price quote | cours | Kurs | cotización |

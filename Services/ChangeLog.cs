namespace Pelago.Services;

/// <summary>Una versione dell'app: numero, data, titolo, racconto e novità.</summary>
public sealed record Release(string Version, DateOnly Date, string Title, string Summary, string[] Story, string[] News);

/// <summary>
/// La storia dell'app, dalla più recente alla più vecchia. È la fonte di verità del numero di versione (Credits, Storia) e va
/// aggiornata a ogni nuova versione: una voce in cima, e <c>&lt;Version&gt;</c> in Pelago.csproj allineata.
///
/// Regola dei numeri: la prima cifra cambia solo per un rifacimento importante; la seconda per ogni gruppo di novità;
/// niente terza cifra. Le versioni 0.x e 1.x sono state assegnate a posteriori, ricostruendo l'ordine del lavoro.
/// </summary>
public static class ChangeLog
{
    public static Release Current => Releases[0];

    public static readonly IReadOnlyList<Release> Releases = new[]
    {
        new Release("2.18", new DateOnly(2026, 10, 13), "Avvertenze più chiare",
            "Una finestra «Ho capito» alla prima visita della parte fiscale, una frase sulla responsabilità in Crediti e un avviso nei file Excel dei prospetti fiscali.",
            new[]
            {
                "Gli importi dello Zainetto fiscale sono stime e c'era già un riquadro che lo diceva, ma chi usa Pelago per la dichiarazione dei redditi merita un avviso che non si possa saltare. Ora la prima volta che apri una pagina del gruppo Fisco, con un file caricato, compare una finestra che spiega i limiti e chiede un «Ho capito»; poi non si ripete. Nella pagina Crediti c'è una frase sulla responsabilità (il programma è fornito senza garanzie, e la responsabilità di ciò che si dichiara resta di chi lo dichiara), e il riquadro dello Zainetto fiscale lo ripete in modo più netto.",
                "I file esportati viaggiano senza l'app, e con loro deve viaggiare l'avviso: l'Excel dello Zainetto fiscale e delle plusvalenze ora ha in fondo una riga di avvertenza. Tra «Come sono calcolati i numeri» abbiamo aggiunto ciò che il prospetto non gestisce: IVAFE e quadro RW, imposte estere, regime fiscale, altre operazioni societarie.",
            },
            new[]
            {
                "Finestra «Ho capito» la prima volta che si apre una pagina del gruppo Fisco (Zainetto fiscale, Costi e tasse, Cedole e scadenze)",
                "Pagina Crediti: «Avvertenze e responsabilità», con la frase sulla responsabilità",
                "Zainetto fiscale: l'avviso in cima dice più chiaramente che la responsabilità di ciò che si dichiara è dell'utente",
                "L'Excel dello Zainetto fiscale e delle plusvalenze ha in fondo una riga di avvertenza",
                "«Come sono calcolati i numeri» elenca anche ciò che non è gestito (IVAFE, quadro RW, imposte estere, regime fiscale)",
                "Nuovi test automatici per la finestra, per l'avviso nell'Excel e per il conto di esempio",
            }),
        new Release("2.17", new DateOnly(2026, 10, 13), "I BTP con nome e bandiera",
            "I BTP del conto prendono il nome di Borsa Italiana, e nella colonna Emittente le obbligazioni mostrano il paese con la sua bandiera.",
            new[]
            {
                "I nomi delle obbligazioni nel file del broker sono in tedesco e dicono solo mese e anno («Okt. 2054»). Borsa Italiana scrive «BTP 4,3% Ott54», con dentro anche la cedola e la scadenza. Ora, quando l'elenco dei BTP si aggiorna, i BTP che hai nel conto prendono il nome di Borsa Italiana nelle liste: come per i derivati, vale sempre prima il nome che scegli tu, e il tuo CSV non si tocca. Il nome resta ricordato anche dopo la scadenza. Un BTP con cedola e scadenza nel nome è già unico, e non serve l'etichetta del file in coda.",
                "Nella colonna «Emittente» le obbligazioni mostrano il paese, ricavato dall'inizio del codice ISIN (IT → Italia), con la sua bandiera: anche nella scheda del titolo, nei nomi, nelle posizioni chiuse e nello zainetto fiscale. L'elenco dei BTP si scarica anche con «Aggiorna tutto» e all'apertura dell'app insieme ai prezzi, ma solo se nel conto ci sono BTP: è una richiesta sola a Borsa Italiana.",
            },
            new[]
            {
                "I BTP del conto prendono il nome di Borsa Italiana (per esempio «BTP 4,3% Ott54») al posto di quello del file",
                "Nella colonna Emittente le obbligazioni mostrano il paese con la bandiera",
                "L'elenco dei BTP si aggiorna anche con «Aggiorna tutto» e all'apertura, solo se nel conto ci sono BTP",
                "Nuovi test automatici per i nomi dei BTP e per il paese delle obbligazioni",
            }),
        new Release("2.16", new DateOnly(2026, 10, 13), "Un conto per provare",
            "Senza un file caricato, la Panoramica avverte che i valori sono stime e permette di provare Pelago con un conto di esempio.",
            new[]
            {
                "Chi apre Pelago per la prima volta, o ha perso il file, vedeva solo un riquadro vuoto. Ora la Panoramica dice cosa fare, ricorda che i dati restano sul PC, che Pelago non è affiliato al broker e che i valori sono stime, e che il codice è libero.",
                "«Prova con un conto di esempio» apre una seconda finestra con un conto inventato: nomi, codici e importi sono finti, e il conto sta in una cartella a parte, così non tocca mai i tuoi dati; chiusa la finestra non resta nulla. In più abbiamo sistemato il pulsante «Apri CSV…» al centro della pagina vuota: la sua icona veniva ingrandita come quella grande sopra, e allargava il pulsante spostando il testo.",
            },
            new[]
            {
                "La Panoramica senza file ricorda che i valori sono stime e che Pelago non è affiliato al broker",
                "Pulsante «Prova con un conto di esempio»: seconda finestra con dati inventati, in una cartella a parte",
                "Corretto il pulsante «Apri CSV…» al centro della pagina vuota (l'icona era troppo grande)",
                "Nuovi test automatici per il conto di esempio",
            }),
        new Release("2.15", new DateOnly(2026, 10, 13), "Tutto si chiama Pelago",
            "Il titolo della finestra mostra la versione, e anche la cartella dei dati sul PC si chiama Pelago.",
            new[]
            {
                "Nel titolo della finestra ora c'è il numero di versione, preso dalla Storia: non resta indietro a ogni aggiornamento. Si vede anche nella barra delle applicazioni, utile quando si hanno più finestre aperte o si deve dire a qualcuno quale versione si usa.",
                "Abbiamo anche dato lo stesso nome a tutto il resto: il progetto, il codice e la cartella in cui l'app salva i dati sul PC, che ora è %LOCALAPPDATA%\\Pelago. Chi aveva già un archivio di prezzi e nomi in un'altra cartella può copiarne il contenuto in questa; il file CSV delle operazioni non si sposta mai, perché l'app non lo modifica.",
            },
            new[]
            {
                "Il titolo della finestra mostra la versione",
                "La cartella dei dati sul PC è %LOCALAPPDATA%\\Pelago",
            }),
        new Release("2.14", new DateOnly(2026, 10, 12), "Le crypto in una richiesta sola",
            "Le crypto si chiedono a CoinGecko tutte insieme: prima le ultime della lista restavano spesso senza prezzo.",
            new[]
            {
                "Il sito che dà i prezzi delle crypto accetta solo poche richieste ravvicinate: nelle prove, dopo le prime sei in un secondo rifiutava le altre con l'errore «troppe richieste». L'app ne faceva una per ogni moneta, e restavano senza prezzo proprio le ultime della lista, cioè le crypto comprate più di recente, mentre le più vecchie andavano bene. Era una causa diversa da quella della versione 2.13, e si vedeva solo con più crypto in portafoglio.",
                "Ora tutte le crypto si chiedono con una sola richiesta, e i prezzi tornano tutti insieme. Se il sito dice comunque «troppe richieste», l'app lo scrive chiaramente nel registro e suggerisce di riprovare tra un minuto, invece di mostrare un errore tecnico. Un id scritto male nel catalogo non viene mandato al sito.",
            },
            new[]
            {
                "Una richiesta sola a CoinGecko per tutte le crypto, invece di una per moneta",
                "Se CoinGecko limita le richieste, il registro lo dice in chiaro e suggerisce di riprovare tra un minuto",
                "Un id CoinGecko scritto male nel catalogo viene segnalato e non mandato al sito",
                "Nuovi test automatici sulla richiesta unica, sull'errore «troppe richieste» e sulla ricerca dell'id",
            }),
        new Release("2.13", new DateOnly(2026, 10, 12), "Crypto che si trovano da sole",
            "Le crypto che l'app non conosceva trovano da sole il loro id su CoinGecko, e quindi il prezzo.",
            new[]
            {
                "Alcune crypto del conto restavano senza prezzo. La causa era un elenco scritto a mano dei simboli che l'app sapeva cercare su CoinGecko: se un simbolo non c'era, nessun prezzo; e una voce creata prima che il simbolo entrasse nell'elenco restava senza id per sempre. C'è anche una trappola: l'id che serve per i prezzi non è quello che si legge nell'indirizzo della pagina della moneta, e con l'id sbagliato si ottiene il prezzo di un'altra moneta, migliaia di volte più basso.",
                "Ora, quando una crypto non ha ancora un id, l'app lo cerca su CoinGecko dal simbolo, sceglie la moneta con quel simbolo esatto e la capitalizzazione più alta, e lo ricorda. Se trova solo monete senza capitalizzazione non sceglie nulla, perché un prezzo che manca è meglio di quello di una moneta sbagliata. Nel registro dell'aggiornamento si legge a quale moneta è stata associata, così si può controllare; se non è quella giusta, si corregge in catalog.json. Le voci già create e senza id vengono completate.",
            },
            new[]
            {
                "Una crypto senza id CoinGecko lo cerca da sola la prima volta, dal simbolo, e lo ricorda",
                "Il registro dell'aggiornamento dice a quale moneta è stata associata, per poterla controllare",
                "Le voci del catalogo create prima, senza id, vengono completate",
                "Nuovi test automatici per la scelta dell'id e per il completamento del catalogo",
            }),
        new Release("2.12", new DateOnly(2026, 10, 12), "Codice aperto",
            "Pelago diventa software libero con licenza GPL-3.0: nella pagina Crediti compaiono la licenza, l'avviso di assenza di garanzia e i componenti di terzi.",
            new[]
            {
                "Abbiamo deciso di pubblicare il codice. Tra MIT e GPL-3.0 ha vinto la GPL-3.0: chiunque può usare, studiare e modificare Pelago, ma chi ne distribuisce una versione modificata deve lasciare il codice aperto, così resta verificabile che i dati non escono dal PC. La licenza non cambia nulla per chi usa l'app: resta gratuita e senza pubblicità.",
                "Nella pagina Crediti c'è ora una sezione «Licenza e codice sorgente» con il copyright e l'avviso di assenza di garanzia, e nell'elenco dei componenti di terzi è comparso anche il carattere dei testi.",
            },
            new[]
            {
                "Pagina Crediti: nuova sezione «Licenza e codice sorgente» (GPL-3.0, nessuna garanzia, copyright)",
                "Il carattere Instrument Sans (SIL Open Font License) è nell'elenco dei componenti open source",
            }),
        new Release("2.11", new DateOnly(2026, 10, 12), "Lo slider con più calma",
            "La riproduzione della mappa va più piano e senza sovrapposizioni; in Composizione a una data lo slider prende il posto di data e pulsanti.",
            new[]
            {
                "Provando la riproduzione a velocità normale, la mappa non faceva in tempo a disegnarsi: i passi arrivavano ogni mezzo secondo scarso, uno sopra l'altro. Ora ogni passo aspetta davvero la fine del precedente e poi lascia un secondo per guardare la mappa; a ×2 e ×4 si va più veloci accorciando l'attesa, non ingrandendo il salto di giorni.",
                "In Composizione a una data abbiamo tolto il selettore della data e i pulsanti «Giorno prec.», «Giorno succ.» e «Oggi»: lo slider fa lo stesso lavoro, e con le frecce della tastiera ci si sposta di un giorno alla volta.",
            },
            new[]
            {
                "Riproduzione più lenta e regolare: un passo al secondo a ×1, mezzo secondo a ×2, un quarto di secondo a ×4",
                "I passi della riproduzione non si accavallano più: ognuno aspetta la fine del precedente",
                "In «Composizione a una data» non ci sono più il selettore della data e i pulsanti Giorno prec., Giorno succ. e Oggi: resta lo slider, con le frecce della tastiera per un giorno alla volta",
                "Il totale del conto sta accanto allo slider",
            }),
        new Release("2.10", new DateOnly(2026, 10, 12), "Anche Société Générale",
            "I derivati aperti di Société Générale hanno un prezzo vero e un nome, come quelli di Vontobel; la funzione parte spenta e si accende a parte.",
            new[]
            {
                "Per Société Générale la pagina del prodotto è un guscio vuoto, ma il sito prende i dati da due chiamate pubbliche: una dà il numero interno e il nome del prodotto, l'altra gli ultimi scambi con Geld e Brief. Con quelle l'app legge il prezzo; di sera e nel fine settimana, quando negli ultimi istanti non ci sono scambi, ripiega sull'ultimo scambio della giornata e ne segna l'ora vera.",
                "Le Note Legali di questo sito sono molto più severe di quelle di Vontobel: vietano la riproduzione dei contenuti con qualsiasi mezzo, memorizzazione compresa, senza autorizzazione scritta. Per questo la funzione ha un interruttore suo, separato da quello di Vontobel, parte spenta, e il banner che la propone riporta queste condizioni: accenderla è una scelta tua, sotto la tua responsabilità.",
                "Come per Vontobel, insieme al prezzo si impara anche il nome: «Turbo Open-End su Oro» prende il posto del nome ricavato dal CSV, con l'etichetta del file in coda. Vale sempre prima il nome che hai scelto tu.",
            },
            new[]
            {
                "Prezzi (Geld e Brief) dei derivati aperti di Société Générale, con un interruttore separato da quello di Vontobel; il pulsante «Derivati» aggiorna quelli accesi",
                "Il nome del prodotto letto dal sito di Société Générale prende il posto di quello del CSV",
                "Di sera e nel fine settimana si usa l'ultimo scambio della giornata, con la sua ora",
                "Il banner di attivazione riporta le Note Legali del sito",
                "Nuovi test automatici per la lettura dei prezzi di Société Générale",
            }),
        new Release("2.9", new DateOnly(2026, 10, 12), "La mappa del portafoglio",
            "I titoli diventano riquadri colorati: più grande è il riquadro, più vale; il colore dice se ci guadagni o ci perdi. Con lo slider si viaggia nel tempo.",
            new[]
            {
                "Volevamo vedere il portafoglio con un'occhiata, senza leggere una tabella. Ogni titolo è un riquadro: la grandezza è il valore, il colore dice quanto stai guadagnando o perdendo rispetto a quanto l'hai pagato, dal rosso al verde, al massimo a ±50%, perché i turbo si muovono di centinaia di punti e con una scala più larga tutto il resto resterebbe grigio. I riquadri stanno raggruppati per categoria e un clic apre i movimenti del titolo.",
                "Nella Panoramica la mappa prende il posto della lista delle posizioni più grandi: i riquadri più grandi sono proprio quelle, e la lista completa con tutti i numeri resta in Titoli. In più c'è una pagina tutta sua, «Mappa del portafoglio», con uno slider per spostarsi nel tempo e un pulsante «Riproduci» che fa scorrere i giorni da soli: si vedono i titoli comparire, crescere e sparire.",
                "Lo slider è lo stesso di «Composizione a una data», e il giorno scelto vale per le due pagine: ci si sposta nel tempo una volta sola. Per le date passate il valore è quello che l'app conosce (le quotazioni registrate e i prezzi dei tuoi scambi): dove mancano è approssimato, e lo scriviamo sotto la mappa.",
            },
            new[]
            {
                "Mappa del portafoglio nella Panoramica, al posto della lista delle posizioni aperte più grandi",
                "Nuova pagina «Mappa del portafoglio» (gruppo Oggi): riquadri per titolo raggruppati per categoria, colore dal rosso al verde sul guadagno rispetto al costo, numeri passando con il mouse, clic per aprire il titolo",
                "Slider del tempo con il pulsante «Riproduci» e la velocità ×1, ×2 o ×4",
                "Lo stesso slider in «Composizione a una data», con il giorno condiviso tra le due pagine",
                "Nuovi test automatici per la mappa, i colori e lo slider",
            }),
        new Release("2.8", new DateOnly(2026, 10, 12), "Chi ha emesso cosa",
            "In ogni lista di titoli compare l'emittente dei derivati, e i prodotti di Vontobel prendono il nome che gli dà il sito dell'emittente.",
            new[]
            {
                "I derivati del conto vengono tutti da due emittenti, Vontobel e Société Générale, e si riconoscono dal prefisso del codice ISIN: non serve andare in rete, quindi vale anche per i prodotti già chiusi. Abbiamo aggiunto la colonna «Emittente» a Titoli, Plusvalenze, Posizioni chiuse, Composizione a una data, Zainetto fiscale, Quotazioni e Nomi dei titoli, e il nome compare anche nella scheda del titolo. Per azioni e fondi resta vuota, perché alcune azioni tedesche hanno un ISIN che comincia come quello dei derivati.",
                "Il sito di Vontobel, insieme al prezzo, dice anche su quale titolo è il prodotto. L'app impara quel nome durante l'aggiornamento (nessuna richiesta in più) e lo ricorda: «Turbo su Acme (Long 150,00 $)» prende il posto di «Turbo auf ACME CORP. DL-,01 (Long 150,00 $)», con l'etichetta del file in coda per distinguere un prodotto dall'altro. Vale il tuo nome se ne hai scelto uno, poi quello dell'emittente, poi quello del file. Nella pagina Nomi dei titoli c'è una colonna apposta.",
                "Per i prodotti di Société Générale non lo facciamo ancora: le loro Note Legali vietano la copia dei contenuti con qualsiasi mezzo senza autorizzazione scritta, e prima di leggere i loro prezzi e i loro nomi vogliamo decidere con calma come muoverci. Nel frattempo i nomi già chiusi restano quelli del file, perché per leggerli bisognerebbe fare un centinaio di richieste ai siti.",
            },
            new[]
            {
                "Colonna «Emittente» (Vontobel, Société Générale) nelle liste dei titoli, ricavata dal prefisso dell'ISIN",
                "Nome dei derivati di Vontobel letto dal sito dell'emittente, che prende il posto del nome ricavato dal CSV; il nome scelto da te vince sempre",
                "Nella pagina Nomi dei titoli la colonna «Nome dell'emittente» e, nella finestra del nome, la riga con quello che dice l'emittente",
                "Correzione: dopo due risposte anomale di fila l'aggiornamento dei derivati ora si ferma davvero, invece di riprovare con il quarto prodotto",
                "Nuovi test automatici sugli emittenti, sui nomi e sull'aggiornamento dei derivati",
            }),
        new Release("2.7", new DateOnly(2026, 10, 12), "Un prezzo vero per i derivati",
            "I derivati aperti di Vontobel hanno finalmente un prezzo vero, letto dal sito dell'emittente, al posto dell'ultimo scambio.",
            new[]
            {
                "In Quotazioni mancavano tutti i derivati, e quelli ancora aperti erano valutati all'ultimo scambio, anche di settimane prima. Abbiamo cercato dove prendere un prezzo vero. Il sito della Borsa di Stoccarda si lascia aprire da un browser ma non da un programma, e leggerlo avrebbe voluto dire aggirare una protezione: abbiamo lasciato perdere. Il sito dell'emittente invece mostra ogni prodotto con un indirizzo che contiene l'ISIN, e dentro la pagina ci sono Geld e Brief.",
                "Trade Republic offre solo derivati di Vontobel e di Société Générale. Quelli di Vontobel, la maggioranza, ora si leggono: l'app manda al sito soltanto il codice del prodotto, una richiesta alla volta con una pausa, dichiarandosi «Pelago», e si ferma se il sito non risponde. La funzione parte spenta: il sito dice di essere per uso privato e rivolto a chi risiede in Germania, Austria, Lussemburgo e Liechtenstein, e scegliere di usarlo tocca a te.",
                "Per quelli di Société Générale la pagina è un guscio senza i prezzi dentro, e serve un altro metodo: per ora restano all'ultimo scambio. Intanto abbiamo sistemato un difetto del convertitore di valute, dove gli elenchi mostravano «_from» e «_to» invece delle valute.",
            },
            new[]
            {
                "Prezzi dei derivati aperti di Vontobel (Geld e Brief) dalla pagina Quotazioni, con il pulsante «Derivati»; la funzione è spenta finché non la attivi",
                "I derivati aperti compaiono nell'elenco delle quotazioni e valgono il Geld, cioè quanto incasseresti vendendo, al posto dell'ultimo prezzo di scambio",
                "Richieste educate al sito: una alla volta, con una pausa, e ci si ferma dopo due risposte anomale di fila",
                "Nomi interi dei titoli nel registro dell'aggiornamento, nell'elenco e nella scheda della quotazione",
                "Convertitore di valute: gli elenchi partono da EUR e USD e il risultato mostra le bandiere",
                "Il controllo automatico delle pagine segnala anche i nomi di campo scritti al posto dei valori",
                "Nuovi test automatici per la lettura dei prezzi Vontobel",
            }),
        new Release("2.6", new DateOnly(2026, 10, 11), "Un nome tuo, un menu più ordinato",
            "Ogni titolo può avere il nome che preferisci, il menu si raccoglie in cinque gruppi e l'aspetto cambia: colori, caratteri e tabelle.",
            new[]
            {
                "Chi usa Pelago tutti i giorni lo sa: i nomi dei turbo sono lunghi e quasi tutti uguali. Ora ogni titolo può avere il nome che vuoi tu. Basta cliccare un codice, in qualsiasi pagina, e scrivere il nome: da quel momento quel codice si chiama così ovunque, anche nella ricerca e negli export. I nomi stanno in un file tutto loro, finiscono nel backup, e c'è una pagina «Nomi dei titoli» per rivederli e cambiarli. I CSV del broker non si toccano mai.",
                "Poi l'aspetto. Il menu di venti voci si è raccolto in cinque gruppi che si aprono e si chiudono, uno alla volta, e quello della pagina in cui sei si apre da solo. I colori sono quelli di una carta nautica, con il verde mare per i guadagni e il vermiglio per le perdite; il carattere è Instrument Sans, incorporato nel programma. Nelle tabelle il risultato ha una barra verso destra o verso sinistra, e le righe possono farsi più compatte.",
                "Infine un po' di ordine dietro le quinte: Posizioni chiuse mostra anche i cicli già chiusi dei titoli che hai ricomprato, e un controllo automatico apre ogni pagina e verifica che le tabelle si vedano e che ogni codice sia cliccabile. Se una pagina sbaglia, ora lo sappiamo prima di te.",
            },
            new[]
            {
                "Nomi dei titoli: clicca un ISIN o un ticker in qualsiasi pagina per dargli il tuo nome; vale ovunque, nella ricerca rapida e negli export",
                "Pagina «Nomi dei titoli» con le tue modifiche, interruttore per vedere tutti i titoli, esporta e importa in CSV; il file dei nomi entra nel backup",
                "Menu in cinque gruppi (Oggi, Operatività, Fisco, Mercati, Dati) che si aprono e si chiudono uno alla volta",
                "Nuovi colori e carattere (Instrument Sans), etichette in minuscolo, menu scuro",
                "Tabelle: barra nel risultato (verso destra per i guadagni, verso sinistra per le perdite) e righe compatte a scelta",
                "Posizioni chiuse: anche i cicli già chiusi dei titoli ricomprati, con il risultato in evidenza",
                "Controllo automatico delle pagine: tabelle visibili, codici cliccabili, nessun errore",
                "I file dell'interfaccia si aggiornano anche quando cambiano senza cambiare lunghezza",
            }),
        new Release("2.5", new DateOnly(2026, 10, 11), "Numeri sotto esame",
            "Statistiche di trading, riscontri sull'affidabilità dello Zainetto fiscale e una revisione del codice che ha tolto una perdita fantasma dai conti.",
            new[]
            {
                "Per chi fa molte operazioni il saldo non basta: serve sapere come si vince. La nuova pagina Statistiche mette in fila quanto spesso guadagni, quanto vale in media un guadagno rispetto a una perdita, quanto durano le posizioni e in quali giorni e mesi rendi di più, con un calendario giorno per giorno. L'unità è l'operazione completa, dal primo acquisto alla vendita finale: contano anche i cicli già chiusi di titoli che poi hai ricomprato.",
                "Poi abbiamo riletto il codice con occhi nuovi, e il file del broker ci ha dato torto in un punto. Una fusione di fondi poi annullata, e alcune vendite annullate e rifatte, venivano lette come vendite vere: bastava una riga per far comparire una perdita che non è mai esistita. Ora quelle righe si compensano e non contano come vendite, e la correzione vale anche per le plusvalenze e per le posizioni chiuse. È la prova che i numeri vanno guardati ogni volta, non una volta sola.",
                "Infine i «riscontri sui numeri»: sotto lo Zainetto fiscale un elenco dice quanto fidarsi del prospetto (costi mancanti, turbo azzerati senza vendita, fondi classificati dal nome) e una tabella confronta le imposte trattenute nel file con quelle del prospetto. Nel frattempo abbiamo sistemato un difetto di impaginazione che nascondeva la tabella delle posizioni chiuse: ora le tabelle non possono più sparire sotto i grafici.",
            },
            new[]
            {
                "Statistiche: percentuale in guadagno, guadagno e perdita medi, fattore di profitto, risultato medio per operazione, commissioni e durata media",
                "Risultato cumulato, risultato per mese, calendario giorno per giorno, risultati per giorno della settimana e per sottostante",
                "Zainetto fiscale: riscontri sui numeri (costo mancante, turbo azzerati, fondi dedotti dal nome, fusioni e storni, cripto gratis) e imposte trattenute per anno confrontate con il prospetto",
                "Le fusioni di fondi e le vendite annullate non contano più come vendite (Zainetto fiscale, Posizioni chiuse e Plusvalenze)",
                "Gli incassi dell'esercizio dei warrant vanno a un solo ciclo",
                "Le tabelle di Posizioni chiuse e del dettaglio titolo non possono più sparire sotto schede e grafici",
                "Aggiunta dei movimenti più robusta: colonne duplicate nel CSV e due aggiunte contemporanee",
                "Nuovi test automatici per statistiche, riscontri fiscali e correzioni",
            }),
        new Release("2.4", new DateOnly(2026, 10, 9), "Più storia, più ordine",
            "Aggiungi solo i movimenti nuovi, le posizioni chiuse con il loro risultato e uno zainetto fiscale informativo anno per anno.",
            new[]
            {
                "Fin qui, per aggiornare i dati bisognava riscaricare dal broker tutti i movimenti, ogni volta. Ora si può scaricare solo l'ultimo mese e aggiungerlo: l'app riconosce dall'identificativo di transazione le operazioni che ha già, aggiunge solo quelle nuove e avvisa se tra i due file potrebbe mancare un periodo. L'insieme di tutti i file aggiunti diventa lo «storico delle operazioni», che l'app usa da lì in poi; i tuoi CSV non vengono mai toccati e prima di ogni aggiornamento se ne tiene una copia.",
                "Poi un posto per le posizioni chiuse: i titoli su cui hai venduto tutto, con il risultato dal primo acquisto all'ultima vendita. Se ricompri lo stesso titolo è un nuovo ciclo, ma nel dettaglio del titolo si vede anche quanto hai fatto in tutto nel tempo. I turbo finiti a zero (knock-out) contano come chiusi in perdita.",
                "Infine lo zainetto fiscale: anno per anno, cosa potresti dover dichiarare diviso per tipo di reddito, con le imposte che il broker ha già trattenuto in evidenza e le minusvalenze da riportare nei quattro anni successivi. È solo informativo: non è consulenza fiscale e non calcola l'imposta dovuta, ma mette in fila i numeri da portare al commercialista, vendita per vendita.",
            },
            new[]
            {
                "Aggiungi movimenti: unisci allo storico un altro CSV (per esempio solo l'ultimo mese); le operazioni già presenti vengono riconosciute e si aggiungono solo le nuove",
                "Esito dell'aggiunta con nuove operazioni, già presenti e conflitti, e avviso se tra i due insiemi potrebbe mancare un periodo; copia di sicurezza ed esportazione dello storico in CSV",
                "Posizioni chiuse: filtri per categoria e anno, risultato per anno di chiusura, turbo raggruppabili per sottostante e imposte già pagate",
                "Cicli di una posizione nel dettaglio del titolo, con il totale nel tempo quando è stata riaperta",
                "Zainetto fiscale: redditi per tipo e per anno, zainetto delle minusvalenze (quattro anni, a parte per le cripto-attività), imposte già trattenute, elenco delle vendite con il costo abbinato da esportare",
                "Il convertitore delle valute ha l'elenco con le bandiere",
                "Nuovi test automatici per lo storico delle operazioni, le posizioni chiuse e il prospetto fiscale",
            }),
        new Release("2.3", new DateOnly(2026, 10, 9), "Cambi, Stati e lingue",
            "Una pagina per i cambi quasi in tempo reale, i titoli di Stato di tanti Paesi e l'app in cinque lingue.",
            new[]
            {
                "Il primo pensiero è andato ai cambi. Per sapere quanto vale un dollaro non serve aspettare il tasso del giorno: abbiamo cercato la fonte gratuita più fresca e l'abbiamo trovata in Yahoo Finance, che dà il cambio aggiornato al secondo; la BCE resta il ripiego ufficiale e fornisce lo storico per i grafici. Avevamo valutato anche il sito OANDA, ma non ha un'API gratuita e i suoi termini vietano di leggere i cambi con un programma, quindi l'abbiamo lasciato stare. Lo stesso cambio quasi in tempo reale ora serve anche a convertire i prezzi in valuta estera, così prezzo e cambio sono dello stesso momento.",
                "Poi i titoli di Stato. La voce «BTP» è diventata «Titoli di Stato»: si sceglie lo Stato e si vedono il rendimento a 10 anni, la curva dei rendimenti, lo spread sui Bund e i propri titoli di quello Stato. Per Germania, Stati Uniti e area euro il dato è giornaliero; per gli altri Stati europei è la media mensile pubblicata dalla BCE. Per i prezzi dei singoli titoli fuori dall'Italia non esiste una fonte gratuita e ufficiale, e abbiamo preferito dirlo piuttosto che inventare numeri: per i BTP resta l'elenco di Borsa Italiana con il calcolatore.",
                "Infine le lingue. I testi dell'app sono nati in italiano; ora si possono leggere anche in inglese, francese, tedesco e spagnolo, con numeri, date e importi scritti come si usa in ciascun paese. La lingua si cambia dal menu a sinistra e vale subito. Un controllo automatico verifica che non manchi nessuna frase tradotta.",
            },
            new[]
            {
                "Valute: i cambi principali contro l'euro quasi in tempo reale (Yahoo Finance, con ripiego sui cambi di riferimento della BCE), grafico storico, convertitore e quanta parte del conto è in valute diverse dall'euro",
                "Il cambio quasi in tempo reale serve anche a convertire i prezzi in valuta estera durante l'aggiornamento delle quotazioni",
                "Titoli di Stato: si sceglie lo Stato (Italia, Germania, Francia, Spagna, Stati Uniti, area euro e gli altri Stati europei)",
                "Rendimento a 10 anni, curva dei rendimenti, spread sui Bund e aste del Tesoro USA, da fonti ufficiali gratuite (BCE, Bundesbank, Tesoro USA)",
                "I tuoi titoli di ciascuno Stato, riconosciuti dall'ISIN, con valore e plus/minus latente",
                "L'app parla italiano, inglese, francese, tedesco e spagnolo: si cambia dal menu a sinistra",
                "Numeri, date e importi nel formato della lingua scelta; ricerca rapida e assistente funzionano anche nella nuova lingua",
                "Le bandiere dei Paesi accanto alle valute e agli Stati dei titoli di Stato",
                "Assistente: la spiegazione completa si vede sempre (niente più «Altro…») e il contesto cambia solo se ti fermi su un elemento, non quando lo sfiori per raggiungere il pannello",
                "Nuovi test automatici per le fonti dei dati e per le traduzioni",
            }),
        new Release("2.2", new DateOnly(2026, 10, 9), "Numeri più veri",
            "Riquadri cliccabili, rendimenti veri, costi, controlli sui dati e la correzione di un prezzo in sterline letto come euro.",
            new[]
            {
                "Tutto è cominciato da due osservazioni su un programma ormai in uso ogni giorno. La prima: i riquadri della Panoramica sembravano pulsanti, ma non lo erano. Ora un clic su «Titoli», «Obbligazioni» o «Crypto» porta alla pagina Titoli già filtrata, e lo stesso vale per tutti gli altri riquadri. È un'idea nata dall'uso, e dice molto su come cresce quest'app: non da un piano, ma da quello che si nota guardandola.",
                "La seconda osservazione era un numero che non tornava: un ETF risultava tra le posizioni più grandi, ma valeva molto meno. La causa era sottile: Yahoo aveva dato il prezzo in pence sterline (4.433 pence sono 44,33 sterline) e l'app lo aveva letto come 4.433 euro; lo stesso succedeva a un titolo quotato in dollari australiani. Ora ogni prezzo non in euro viene convertito con il cambio giusto, i prezzi sbagliati già salvati sono stati riparati da soli, e c'è una nuova pagina di controlli che avrebbe segnalato il problema il primo giorno.",
                "Da lì abbiamo fatto una lista di tutto ciò che mancava per capire davvero come va il conto, e l'abbiamo fatta tutta: il rendimento che tiene conto di quando hai versato, quanto hai speso in costi, quanto guadagneresti vendendo oggi, il calendario delle cedole. Insieme sono arrivati dei test automatici per i calcoli, perché dopo il prezzo in sterline la lezione era chiara: i numeri vanno verificati ogni volta, non una volta sola.",
            },
            new[]
            {
                "Panoramica: tutti i riquadri sono cliccabili e portano alla pagina giusta già filtrata; clic sulle fette della ciambella; nuovi indicatori (plus/minus latente, rendimento, costi, concentrazione) e un obiettivo di patrimonio con la linea sul grafico",
                "Nomi interi nelle posizioni più grandi (per esempio «VanEck Semiconductor UCITS ETF» invece del nome breve)",
                "Correzione dei prezzi in valuta estera (sterline, pence, dollari australiani...) e riparazione di quelli già salvati",
                "Titoli: filtri multipli, costo medio, costo totale, plus/minus latente e peso; segnale per i prezzi vecchi",
                "Rendimenti: rendimento ponderato nel tempo (TWR) e annuo sui tuoi soldi (XIRR), anno per anno e per categoria",
                "Costi e tasse: commissioni e imposte per mese e per anno",
                "Controlli: verifiche automatiche sui dati, con un avviso nel menu e nella Panoramica",
                "Cedole e scadenze: il calendario degli incassi dei BTP che possiedi; confronto affiancato di 2 o 3 BTP",
                "Aggiornamento dei prezzi da solo all'apertura, ricarica del CSV quando cambia e avviso se nella cartella arriva un file più recente",
                "Backup automatico settimanale in una cartella a tua scelta",
                "Ricerca rapida (Ctrl+K) e modalità privacy (F2) che sfoca gli importi",
                "Esporta in Excel (vero .xlsx) da tutte le tabelle e in PDF dalla Panoramica",
                "Test automatici per i calcoli (rendimenti, costi, costo medio, valute, cedole)",
            }),

        new Release("2.1", new DateOnly(2026, 10, 8), "Si spiega da sola",
            "Nasce il nome Pelago, con la sua icona, e l'app impara a spiegarsi: modalità assistita, Dati e file, Credits e questa Storia.",
            new[]
            {
                "Con la nuova interfaccia l'app era diventata bella, ma restava una domanda: chi la apre tra sei mesi ricorderà cosa significa «rateo per 100» o da dove arrivano i prezzi? Da qui l'idea di un assistente che risponde mentre si guarda una colonna, e a cui si può anche scrivere. Abbiamo ragionato a lungo sul fatto che potesse essere un vero chatbot: la scelta è stata di farlo restare tutto sul tuo PC, senza che nulla esca verso terzi. Un'intelligenza artificiale collegata ai tuoi dati potrà arrivare, se e quando lo vorrai.",
                "Nello stesso giorno ci siamo accorti che un'app così curata meritava un nome vero. Ne abbiamo considerati quattro (Pelago, Quota, Bussola, Fiorino) e ha vinto Pelago: in italiano è il mare aperto, e rende bene l'idea di navigare tra i propri soldi. L'icona è una barca a vela sulle onde. Il file è diventato pelago.exe, tutto minuscolo.",
                "Dopo il rifacimento, la vecchia interfaccia è stata abbandonata: la teniamo nel progetto, ma non la aggiorniamo più.",
            },
            new[]
            {
                "Modalità assistita: pannello in basso a destra che spiega colonne, schede, pulsanti e grafici mentre li guardi (F1 per accenderla)",
                "Chat dell'assistente: scrivi «cosa vuol dire tel quel?» e ti risponde, senza internet e senza inviare nulla a nessuno",
                "Dati e file: dove sono salvati quotazioni, catalogo e preferenze; apertura della cartella e backup in un file zip",
                "Credits: fonti dei dati, componenti open source con le licenze e avvertenze",
                "Storia delle versioni (questa pagina)",
                "Nuovo nome Pelago e nuova icona; l'eseguibile si chiama pelago.exe",
            }),

        new Release("2.0", new DateOnly(2026, 10, 8), "Il grande rifacimento",
            "L'interfaccia viene ripensata da zero: pagine moderne, grafici ECharts, tema chiaro e scuro.",
            new[]
            {
                "L'app funzionava bene, ma si vedeva che era fatta di finestre di Windows: tabelle grigie, grafici disegnati a mano. Alla domanda «come la rendiamo più bella?» abbiamo preferito rispondere cambiando strada: un'interfaccia a pagine con un menu a sinistra, i grafici di una libreria professionale (Apache ECharts) e un tema scuro. Prima di costruirla abbiamo disegnato dei bozzetti, e solo dopo la tua conferma abbiamo costruito tutto.",
                "Per non perdere nulla, abbiamo fatto un backup completo del vecchio progetto e tenuto la vecchia interfaccia raggiungibile. Tutti i calcoli (saldi, composizione, plusvalenze, BTP) sono rimasti gli stessi, già verificati: è cambiato solo il modo di mostrarli. Anche l'eseguibile portabile è rimasto un file solo, con tutto l'occorrente al suo interno.",
            },
            new[]
            {
                "Nuova interfaccia con menu laterale e pagine: Panoramica, Andamento giornaliero, Grafici, Composizione a una data, Entrate/Uscite, Interessi, Titoli, Plusvalenze, Quotazioni, BTP",
                "Panoramica con le schede principali, la ciambella della composizione e il grafico totale contro versamenti",
                "Tabelle con filtro, ordinamento ricordato, totali in fondo ed esportazione in CSV",
                "Titoli: un'unica pagina con i filtri Azioni, Fondi, Obbligazioni, Crypto, Derivati al posto di tre menu",
                "Grafici interattivi con legenda cliccabile e zoom; il «cappello» sulle colonne diventa un triangolino verde/rosso",
                "Tema chiaro e scuro",
            }),

        new Release("1.5", new DateOnly(2026, 10, 7), "I BTP",
            "Dal foglio Excel di un calcolatore a una sezione dedicata ai BTP, con dati scaricati online.",
            new[]
            {
                "Tutto è nato da un file Excel, il Calcolatore_BTP: dato un capitale, quanti BTP si comprano e cosa si incassa. L'idea era riportarlo nell'app, ma con una differenza: niente fogli da aggiornare a mano, tutto scaricato online da Borsa Italiana. Il lavoro più delicato è stato verificare che i numeri coincidessero con quelli del foglio, riga per riga, e capire le diverse forme in cui i nomi dei BTP indicano la cedola.",
                "Abbiamo aggiunto due rendimenti in evidenza nell'elenco, ciascuno con il suo colore, e fissato le commissioni a zero per impostazione. Se un domani uscisse un BTP con un formato di nome nuovo, l'app lo segnala invece di ignorarlo in silenzio.",
            },
            new[]
            {
                "Elenco dei BTP a tasso fisso scaricato da Borsa Italiana, aggiornato in automatico se i dati sono vecchi",
                "Colonne di rendimento: cedolare lordo e effettivo a scadenza lordo/netto, con due colori",
                "Calcolatore: capitale, commissioni, aliquota, taglio e data di regolamento, confrontati con «se quotasse 100»",
                "Segnalazione dei titoli non riconosciuti, per accorgersi dei nuovi formati",
            }),

        new Release("1.0", new DateOnly(2026, 9, 25), "Una macchina del tempo",
            "Si può guardare il conto in qualunque giorno del passato, e vedere come è cambiata la sua composizione.",
            new[]
            {
                "Fino ad allora l'app mostrava il conto «di oggi». La nuova domanda era: cosa avevo in portafoglio il mese scorso, o a fine anno? Abbiamo costruito la composizione a una data, con un calendario e categorie colorate da spuntare, e poi il grafico che mette in fila tante date: giornaliero, settimanale, mensile fino all'anno in corso (YTD).",
                "Passandoci sopra col mouse si vede quanto vale ogni categoria e come è variata rispetto alla colonna precedente; un indicatore dice se in quel momento il conto era sopra o sotto a quanto versato. Per questo la chiamiamo 1.0: è la versione in cui il conto smette di essere solo quello di oggi e si può rivivere nel tempo.",
            },
            new[]
            {
                "Composizione a una data: calendario, giorno precedente/successivo, categorie con colori propri",
                "Totale della selezione e dettaglio di ogni asset a quella data",
                "Grafico di composizione a periodi: giornaliero, settimanale, mensile, bimestrale, trimestrale, quadrimestrale, semestrale, annuale, YTD",
                "Variazione di ogni categoria rispetto alla colonna precedente e scostamento sopra/sotto i versamenti",
            }),

        new Release("0.5", new DateOnly(2026, 9, 22), "Sopra o sotto?",
            "I primi grafici, e la domanda che li ha guidati: sono sopra o sotto a quanto ho versato?",
            new[]
            {
                "Con i dati in ordine è arrivata la curiosità di vederli. Abbiamo provato le linee, poi le colonne impilate; ma la domanda vera era un'altra: quanto sono sopra o sotto ai soldi che ho messo? Ne è uscito un grafico a colonne con la linea dei versamenti e un «cappello» verde o rosso per lo scostamento. Alla prima prova il cappello non si vedeva quando il conto era sotto i versamenti: sistemato.",
            },
            new[]
            {
                "Grafico a linee di disponibilità, titoli, obbligazioni, crypto e totale",
                "Colonne impilate mensili",
                "Grafico «sopra/sotto i versamenti» con cappello verde/rosso e linea dei versamenti netti",
            }),

        new Release("0.4", new DateOnly(2026, 9, 22), "I prezzi veri",
            "L'app smette di fidarsi solo dell'ultimo prezzo di un'operazione e va a prendere le quotazioni.",
            new[]
            {
                "Il valore di titoli e crypto si fermava all'ultimo prezzo a cui si era comprato o venduto: giusto, ma vecchio. Ci siamo chiesti se fosse possibile collegare le posizioni aperte alla quotazione di oggi, e prima di scrivere una riga abbiamo ragionato su come: un catalogo di asset con un file di quotazioni ciascuno, così che aggiornarne uno non tocchi gli altri.",
                "Poi i fornitori: CoinGecko per le crypto, Yahoo Finance per azioni e fondi, e Frankfurter per il cambio euro/dollaro, usato per convertire i prezzi in dollari. Un menu «Aggiorna dati» fa partire tutto in parallelo, con un registro che scorre in tempo reale; le finestre ricordano posizione e colonne, come in un'applicazione professionale.",
            },
            new[]
            {
                "Catalogo degli asset e storico delle quotazioni, un file per asset",
                "Aggiornamento di crypto, azioni, fondi e cambio, fino a 5 richieste in parallelo",
                "Registro dell'aggiornamento in una finestra a parte",
                "Elenco quotazioni con ultimo valore e data, e storico completo per asset",
                "Priorità alle quotazioni scaricate rispetto all'ultimo prezzo di un'operazione",
            }),

        new Release("0.3", new DateOnly(2026, 9, 22), "Il primo exe",
            "Un programma che si porta in giro: un solo file, senza installazione.",
            new[]
            {
                "A metà giornata l'app già serviva abbastanza da volerla ovunque. Abbiamo generato il primo eseguibile portabile: un file unico che si copia dove si vuole e si avvia con un doppio clic.",
            },
            new[]
            {
                "Eseguibile unico e portabile, senza installazione",
            }),

        new Release("0.2", new DateOnly(2026, 9, 22), "Movimenti",
            "Dal saldo alle domande successive: da dove arrivano i soldi, quanto rendono, cosa ho in portafoglio?",
            new[]
            {
                "Una volta tornati i numeri, le domande si sono fatte più precise. Da dove sono arrivati i soldi? Quanti interessi ho incassato? Cosa è rimasto in portafoglio, e cosa ho venduto con un guadagno o una perdita? Il menu Movimenti nasce da qui.",
                "Strada facendo abbiamo imparato che un nome come «Long 61,49 $» non dice nulla su un turbo, e abbiamo fatto in modo di ricostruire il nome completo dall'operazione; e che per le posizioni chiuse conta il saldo, quanto hanno dato o tolto in tutta la loro storia.",
            },
            new[]
            {
                "Entrate/Uscite, Interessi, Titoli e Crypto, ciascuno con la riga di saldo finale",
                "Doppio clic su un asset: tutti i suoi movimenti",
                "Nomi completi per i derivati e saldo per le posizioni chiuse",
                "Plusvalenze e minusvalenze realizzate, a costo medio ponderato",
            }),

        new Release("0.1", new DateOnly(2026, 9, 22), "Il saldo che non tornava",
            "L'inizio: un file CSV di migliaia di righe e il desiderio di capire il conto giorno per giorno.",
            new[]
            {
                "Tutto è cominciato con un file: l'esportazione delle operazioni del broker, un CSV troppo lungo per essere letto a occhio. Serviva un programma semplice che lo aprisse e mostrasse, per ogni giorno, quanto valeva il conto diviso tra disponibilità, titoli, obbligazioni e crypto.",
                "La prima versione sbagliava un numero importante: la disponibilità. Non contava commissioni e tasse, e il saldo non coincideva con quello che si vedeva nell'app del broker. È stata la prima lezione, che ci siamo portati dietro: ogni numero va confrontato con la realtà. Corretto quel dettaglio, il saldo è tornato.",
            },
            new[]
            {
                "Apertura del file CSV e riapertura automatica dell'ultimo file usato",
                "Saldo giornaliero diviso per disponibilità, titoli, obbligazioni e crypto",
                "Valutazione degli asset all'ultimo prezzo noto",
            }),
    };
}

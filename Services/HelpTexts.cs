using System.Globalization;
using System.Text;

namespace DPOper.Services;

/// <summary>Una spiegazione: titolo, versione breve, approfondimento (facoltativo: l'assistente lo mostra sempre sotto la versione breve) e parole chiave per la ricerca.</summary>
public sealed record HelpEntry(string Key, string Title, string Short, string? More, string Keywords);

/// <summary>
/// Tutti i testi della modalità assistita, in un solo posto. Nei testi si possono usare segnaposto come {cash} o {total}:
/// li riempie <see cref="AssistState"/> con i valori reali del conto.
///
/// Chiavi: «page:/percorso» = introduzione di una pagina; «nav:...» = voci del menu; «tabella/Titolo colonna» = colonna di una
/// tabella («*/Titolo» vale per tutte le tabelle); le altre sono schede, pulsanti e grafici.
/// </summary>
public static class HelpTexts
{
    private static readonly Dictionary<string, HelpEntry> Entries = Build();

    /// <summary>Tutte le schede, nella lingua corrente.</summary>
    public static IEnumerable<HelpEntry> All => Entries.Values.Select(Localize);

    /// <summary>La scheda nella lingua corrente (i segnaposto come {total} restano intatti: li riempie <see cref="AssistState"/>).</summary>
    public static HelpEntry? Find(string key) => Entries.TryGetValue(key, out var entry) ? Localize(entry) : null;

    /// <summary>I testi sono scritti in italiano e sono anche le chiavi di traduzione (vedi <see cref="Loc"/>). Le parole chiave della ricerca restano valide anche in italiano.</summary>
    private static HelpEntry Localize(HelpEntry entry)
    {
        if (Loc.Lang == "it")
        {
            return entry;
        }

        var keywords = entry.Keywords.Length == 0 ? "" : $"{Loc.T(entry.Keywords)} {entry.Keywords}";
        return new HelpEntry(entry.Key, Loc.T(entry.Title), Loc.T(entry.Short), entry.More is null ? null : Loc.T(entry.More), keywords);
    }

    public static bool Has(string key) => Entries.ContainsKey(key);

    /// <summary>Chiave di aiuto per una colonna: prima quella specifica della tabella, poi quella generica; null se non c'è.</summary>
    public static string? ColumnKey(string tableId, string title)
    {
        var specific = $"{tableId}/{title}";
        if (Entries.ContainsKey(specific))
        {
            return specific;
        }
        var generic = $"*/{title}";
        return Entries.ContainsKey(generic) ? generic : null;
    }

    // ---------------------------------------------------------------- ricerca

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "il", "lo", "la", "le", "gli", "i", "un", "una", "uno", "di", "del", "della", "dei", "delle", "dello", "da", "dal", "dalla",
        "in", "nel", "nella", "con", "su", "sul", "sulla", "per", "tra", "fra", "a", "al", "alla", "e", "o", "ma", "che", "cosa", "cos",
        "come", "significa", "significano", "vuol", "vuole", "dire", "mi", "puoi", "puo", "spiegare", "spiegami", "dimmi", "sono",
        "quale", "quali", "perche", "non", "si", "mio", "mia", "miei", "mie", "questo", "questa", "questi", "queste", "quello", "quella",
        "ce", "c", "cose", "voce", "colonna", "campo", "valore", "ha", "hai", "ho", "fa", "fai", "serve", "usa", "vedo", "vedere",
        "e", "ed", "se", "sto", "stai", "cio", "qui", "ora", "ok", "ciao", "grazie", "vorrei", "sapere", "capire", "dove", "quando",
    };

    /// <summary>Parole da ignorare nelle domande, nelle altre lingue (quelle italiane valgono sempre).</summary>
    private static HashSet<string> ForeignStopWords(string lang) => lang switch
    {
        "en" => EnglishStopWords,
        "fr" => FrenchStopWords,
        "de" => GermanStopWords,
        "es" => SpanishStopWords,
        _ => EmptyWords,
    };

    private static readonly HashSet<string> EmptyWords = new();

    private static readonly HashSet<string> EnglishStopWords = new(StringComparer.Ordinal)
    {
        "the", "a", "an", "of", "what", "whats", "is", "are", "does", "do", "mean", "means", "meaning", "how", "i", "my", "me", "this", "that",
        "in", "on", "for", "to", "and", "or", "can", "you", "explain", "tell", "please", "thanks", "thank", "hello", "hi", "want", "know",
        "understand", "where", "when", "why", "which", "it", "its", "about", "field", "column", "value", "see", "show", "there",
    };

    private static readonly HashSet<string> FrenchStopWords = new(StringComparer.Ordinal)
    {
        "le", "la", "les", "un", "une", "des", "du", "de", "d", "l", "que", "qu", "quoi", "est", "sont", "ce", "cet", "cette", "ces",
        "signifie", "veut", "dire", "comment", "je", "mon", "ma", "mes", "en", "dans", "sur", "pour", "et", "ou", "peux", "peut", "tu", "vous",
        "explique", "expliquer", "dis", "merci", "bonjour", "salut", "veux", "savoir", "comprendre", "ou", "quand", "pourquoi", "il", "y",
        "colonne", "champ", "valeur", "voir", "a", "au", "aux",
    };

    private static readonly HashSet<string> GermanStopWords = new(StringComparer.Ordinal)
    {
        "der", "die", "das", "den", "dem", "des", "ein", "eine", "einen", "einem", "einer", "was", "ist", "sind", "bedeutet", "heisst", "wie",
        "ich", "mein", "meine", "meinen", "in", "im", "auf", "fur", "und", "oder", "kann", "kannst", "du", "sie", "erklare", "erklaren", "sag",
        "danke", "hallo", "will", "wissen", "verstehen", "wo", "wann", "warum", "es", "spalte", "feld", "wert", "sehen", "zu", "zum", "zur", "von",
    };

    private static readonly HashSet<string> SpanishStopWords = new(StringComparer.Ordinal)
    {
        "el", "la", "los", "las", "un", "una", "unos", "unas", "de", "del", "que", "qu", "es", "son", "significa", "quiere", "decir", "como",
        "yo", "mi", "mis", "en", "sobre", "para", "y", "o", "puedes", "puede", "tu", "usted", "explica", "explicar", "dime", "gracias", "hola",
        "quiero", "saber", "entender", "donde", "cuando", "por", "porque", "columna", "campo", "valor", "ver", "esto", "esta", "este", "al",
    };

    private static string Normalize(string text)
    {
        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }
            sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }
        return sb.ToString();
    }

    /// <summary>Parole significative di una domanda (senza articoli, «cosa significa», ecc.).</summary>
    public static List<string> Tokens(string question) => Normalize(question)
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Where(t => t.Length >= 2 && !StopWords.Contains(t) && !ForeignStopWords(Loc.Lang).Contains(t))
        .ToList();

    private static bool Matches(string word, string token)
    {
        if (word == token)
        {
            return true;
        }
        // Prefisso comune (plurali, coniugazioni): "cedole" ~ "cedola", "rendimenti" ~ "rendimento".
        var n = Math.Min(Math.Min(word.Length, token.Length), 5);
        return n >= 4 && word.StartsWith(token[..n], StringComparison.Ordinal);
    }

    /// <summary>Cerca le spiegazioni più vicine alla domanda, dalla migliore. Vuota se non c'è nulla di pertinente.</summary>
    public static List<(HelpEntry Entry, int Score)> Search(string question)
    {
        var tokens = Tokens(question);
        if (tokens.Count == 0)
        {
            return new();
        }

        var results = new List<(HelpEntry, int)>();
        foreach (var entry in All.Where(e => !e.Key.StartsWith("page:", StringComparison.Ordinal)))
        {
            var keywordWords = Normalize(entry.Keywords).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var titleWords = Normalize(entry.Title).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var bodyWords = Normalize(entry.Short).Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var score = 0;
            foreach (var token in tokens)
            {
                if (keywordWords.Any(w => Matches(w, token))) score += 4;
                if (titleWords.Any(w => Matches(w, token))) score += 3;
                if (bodyWords.Any(w => Matches(w, token))) score += 1;
            }

            if (score >= 3)
            {
                results.Add((entry, score));
            }
        }

        return results.OrderByDescending(r => r.Item2).ThenBy(r => r.Item1.Title.Length).ToList();
    }

    // ----------------------------------------------------------------- testi

    private static Dictionary<string, HelpEntry> Build()
    {
        var d = new Dictionary<string, HelpEntry>(StringComparer.Ordinal);

        void E(string key, string title, string shortText, string? more = null, string keywords = "") =>
            d[key] = new HelpEntry(key, title, shortText, more, keywords);

        // ---- Introduzione delle pagine
        E("page:/", "Panoramica",
            "Il conto di oggi in un colpo d'occhio: totale, categorie, quanto sei sopra o sotto ai versamenti e le posizioni più grandi.",
            "Tutti i valori nascono dal file CSV delle operazioni e dagli ultimi prezzi noti. Passa il mouse su una scheda per capire come viene calcolata. Ogni riquadro è cliccabile: porta alla pagina con i dettagli (Titoli, Obbligazioni e Crypto aprono la pagina Titoli già filtrata).");
        E("page:/daily", "Andamento giornaliero",
            "Il saldo del conto giorno per giorno, diviso per tipologia: ogni riga è la fotografia a fine giornata.",
            "Un giorno compare se c'è stata almeno un'operazione o una quotazione aggiornata. Clicca sulle intestazioni per ordinare; il filtro cerca in tutte le colonne.");
        E("page:/charts", "Grafici",
            "Tre modi di vedere il conto nel tempo: le linee dell'andamento, le colonne di composizione e lo scostamento rispetto ai versamenti.",
            "Dalla legenda puoi nascondere una voce con un clic; trascinando la barra in basso ingrandisci un periodo.");
        E("page:/asof", "Composizione a una data",
            "Scegli un giorno e vedi cosa avevi in quel momento: liquidità, titoli, crypto, e così via.",
            "Spunta le categorie a sinistra: le righe si sommano e ognuna ha il colore della sua categoria. Un clic su un asset apre i suoi movimenti fino a quel giorno.");
        E("page:/transfers", "Entrate / Uscite",
            "I bonifici da e verso i tuoi conti esterni: sono i soldi che hai davvero messo e tolto.",
            "Il totale netto è la base per capire se il conto è sopra o sotto quanto hai versato.");
        E("page:/interest", "Interessi",
            "Gli interessi pagati sulla liquidità, con lordo, tassa trattenuta e netto.");
        E("page:/securities", "Titoli",
            "Tutti gli asset che hai movimentato, con quantità e valore all'ultimo prezzo noto. Clicca una riga per vederne i movimenti.",
            "I filtri in alto separano azioni, fondi, obbligazioni, crypto e derivati. «Solo posizioni aperte» nasconde quelle chiuse.");
        E("page:/security", "Movimenti di un asset",
            "Tutta la storia di un singolo asset: acquisti, vendite, ricevute gratuite, esercizi.");
        E("page:/gains", "Plusvalenze",
            "Utile o perdita realizzati su ogni cessione, calcolati a costo medio ponderato.",
            "Non è una dichiarazione fiscale: serve a capire come sono andate le tue operazioni.");
        E("page:/quotes", "Quotazioni",
            "Qui aggiorni i prezzi degli asset e vedi cosa è successo. Da questi prezzi dipende il valore di titoli, obbligazioni e crypto.",
            "Senza aggiornare, l'app usa l'ultimo prezzo di un'operazione, che può essere vecchio.");
        E("page:/quote", "Storico di un asset",
            "Tutte le quotazioni che hai registrato per un asset, con il grafico.");
        E("page:/btp", "Titoli di Stato · Italia (BTP)",
            "L'elenco dei BTP a tasso fisso da Borsa Italiana, con rendimenti già calcolati. Clicca un titolo per simulare l'acquisto. In alto scegli un altro Stato.");
        E("page:/btp/calc", "Calcolatore BTP",
            "Quanti BTP compri con un capitale, quanto incassi di cedole e che surplus hai a scadenza, al prezzo di mercato e come se quotasse 100.",
            "È la versione dell'app del foglio Calcolatore_BTP.xlsx. Cambia gli input in alto: il calcolo si aggiorna subito.");
        E("page:/files", "Dati e file",
            "Dove l'app tiene i suoi file sul tuo PC: quotazioni, catalogo asset, BTP e preferenze.");
        E("page:/credits", "Credits",
            "Chi ha fatto l'app, da dove arrivano i dati e quali componenti open source usa.");

        // ---- Menu
        E("nav:/", "Panoramica", "La pagina iniziale con i numeri principali del conto.");
        E("nav:/daily", "Andamento giornaliero", "La tabella del saldo giorno per giorno.");
        E("nav:/charts", "Grafici", "Andamento, composizione e scostamento in grafico.");
        E("nav:/asof", "Composizione a una data", "Cosa avevi nel conto in un giorno a tua scelta.");
        E("nav:/transfers", "Entrate / Uscite", "I bonifici da e verso i conti esterni.");
        E("nav:/interest", "Interessi", "Gli interessi sulla liquidità.");
        E("nav:/securities", "Titoli", "Tutti gli asset movimentati.");
        E("nav:/gains", "Plusvalenze", "Utili e perdite realizzati.");
        E("nav:/quotes", "Quotazioni", "Aggiorna i prezzi e consulta lo storico.");
        E("nav:/btp", "BTP", "Elenco BTP e calcolatore.");
        E("nav:/files", "Dati e file", "Dove sono salvati i file dell'app, con backup.");
        E("nav:/credits", "Credits", "Autori, fonti dati e licenze.");
        E("nav:theme", "Tema", "Passa dal tema chiaro a quello scuro.");
        E("nav:assist", "Modalità assistita",
            "Accende questo assistente: muovi il mouse su una voce e ti spiego cos'è. Scorciatoia: F1.");
        E("top:open", "Apri CSV", "Sceglie il file CSV delle operazioni (l'esportazione del broker). L'app ricorda l'ultimo file usato.",
            keywords: "file csv apri carica esportazione broker");
        E("top:quotes", "Quotazioni al…", "Quando è stato fatto l'ultimo aggiornamento di una qualsiasi quotazione.",
            keywords: "ultimo aggiornamento quotazioni data");

        // ---- Schede (Panoramica e altre)
        E("total", "Totale conto",
            "Tutto quello che il conto vale oggi: {total}.",
            "Disponibilità + titoli + obbligazioni + crypto. Gli asset sono valutati all'ultimo prezzo noto, quindi è una stima, non un saldo bancario.",
            "totale conto patrimonio valore complessivo");
        E("cash", "Disponibilità",
            "La liquidità sul conto: {cash}.",
            "Somma di importi, commissioni e tasse di tutte le operazioni del CSV: acquisti e vendite incluse. È il saldo in cassa, non il valore degli asset.",
            "disponibilita liquidita cassa contanti saldo");
        E("securities", "Titoli",
            "Azioni, fondi e derivati che possiedi: {securities}.",
            "Per ogni asset: quantità × ultimo prezzo noto. Se hai aggiornato le quotazioni, quel prezzo ha sempre la precedenza sull'ultima operazione.",
            "titoli azioni fondi derivati turbo etf");
        E("bonds", "Obbligazioni", "Il valore delle obbligazioni che possiedi: {bonds}.",
            "Quantità × ultimo prezzo noto, come per i titoli. Il prezzo è una percentuale del valore nominale.",
            "obbligazioni btp bond");
        E("crypto", "Crypto", "Il valore delle crypto che possiedi: {crypto}.",
            "Quantità × ultimo prezzo da CoinGecko (o dall'ultima operazione, se non hai mai aggiornato).",
            "crypto bitcoin criptovalute");
        E("contributions", "Versamenti netti",
            "Quanto hai messo davvero nel conto dall'esterno: {contrib}.",
            "Bonifici in entrata meno bonifici in uscita da/verso conti tuoi. Gli interessi non contano come versamento: restano nel Totale come rendimento.",
            "versamenti netti bonifici messo depositato capitale investito");
        E("gap", "Sopra / sotto i versamenti",
            "Quanto il conto vale più (o meno) di quanto hai versato: {gap} ({gappct}).",
            "Totale − Versamenti netti. La percentuale è rispetto ai versamenti. Attenzione: non tiene conto di quando hai versato, quindi non è un rendimento annuo.",
            "sopra sotto scostamento guadagno perdita differenza versamenti rendimento");
        E("interest.card", "Interessi netti incassati", "Gli interessi sulla liquidità, già al netto delle tasse: {interest}.",
            keywords: "interessi netti incassati");
        E("realized.card", "Plus/minus realizzate", "Il risultato di tutte le cessioni fatte: {realized}.",
            "Calcolo a costo medio ponderato. Un turbo scaduto o un warrant esercitato conta come venduto a zero: il costo residuo diventa una minusvalenza. Non è un calcolo fiscale.",
            "plusvalenze minusvalenze realizzate utile perdita");
        E("home.heatmap", "Mappa del portafoglio",
            "Un riquadro per ogni titolo che possiedi: più è grande, più vale. Il colore dice se ci stai guadagnando (verde) o perdendo (rosso) rispetto a quanto l'hai pagato. Clicca un riquadro per vedere i movimenti di quel titolo.",
            "Prende il posto della vecchia lista delle posizioni più grandi: i riquadri più grandi sono proprio quelle. Per la lista completa con tutti i numeri c'è la pagina Titoli.", "mappa heat map posizioni peso concentrazione colori");

        // ---- Schede movimenti
        E("transfers.in", "Entrate", "La somma dei bonifici in arrivo.", keywords: "entrate bonifici arrivo versato");
        E("transfers.out", "Uscite", "La somma dei bonifici in uscita verso conti esterni.", keywords: "uscite prelievi bonifici");
        E("transfers.net", "Netto versato", "Entrate meno uscite: è il numero dei «Versamenti netti» usato nei confronti.", keywords: "netto versato");
        E("count", "Numero di movimenti", "Quante righe ci sono nella tabella qui sotto.", keywords: "movimenti numero righe");
        E("interest.gross", "Interessi lordi", "L'interesse prima delle tasse.", keywords: "lordo interessi");
        E("interest.tax", "Tasse trattenute", "L'imposta trattenuta dal broker sugli interessi.", keywords: "tasse imposta trattenute");
        E("interest.net", "Netto incassato", "Quello che è davvero arrivato in cassa: lordo meno tasse.", keywords: "netto incassato");
        E("gains.total", "Totale realizzato", "La somma di utili e perdite di tutte le cessioni.", keywords: "totale realizzato");
        E("gains.wins", "Cessioni in utile", "Quante vendite sono state chiuse con un guadagno.", keywords: "utile guadagno cessioni");
        E("gains.losses", "Cessioni in perdita", "Quante vendite sono state chiuse in perdita (compresi i turbo scaduti).", keywords: "perdita cessioni");
        E("sec.shares", "Quantità netta", "Quante unità possiedi oggi: acquisti meno vendite.", keywords: "quantita netta posseduta");
        E("sec.value", "Valore stimato", "Quantità × ultimo prezzo noto.", keywords: "valore stimato");
        E("sec.net", "Netto in cassa",
            "Quanto questo asset ha dato (o tolto) in cassa in tutta la sua storia: vendite incassate meno acquisti, commissioni e tasse comprese.",
            keywords: "netto cassa");

        // ---- Colonne generiche
        E("*/Data", "Data", "Quando è avvenuta l'operazione (o il giorno a cui si riferisce la riga).", keywords: "data giorno quando");
        E("*/Nome", "Nome", "Il nome dell'asset. Per i derivati l'app ricostruisce il nome completo del prodotto dalla descrizione delle operazioni.", keywords: "nome asset");
        E("*/Simbolo", "Simbolo", "Il codice dell'asset: di solito l'ISIN, per le crypto la sigla (BTC, ETH…).", keywords: "simbolo isin codice ticker");
        E("*/Categoria", "Categoria", "Il tipo di asset: azione, fondo, obbligazione, crypto, derivato.", keywords: "categoria tipo classe");
        E("*/Quantità", "Quantità", "Quante unità: azioni, quote di fondo, monete o titoli.", keywords: "quantita unita pezzi");
        E("*/Descrizione", "Descrizione", "Il testo dell'operazione così come lo scrive il broker.", keywords: "descrizione testo");
        E("*/Importo", "Importo", "L'importo dell'operazione. Negativo = soldi usciti dal conto, positivo = entrati.", keywords: "importo");
        E("*/Disponibilità", "Disponibilità", "Liquidità a fine giornata. Vedi la scheda «Disponibilità» in Panoramica.", keywords: "disponibilita liquidita cassa");
        E("*/Titoli", "Titoli", "Valore di azioni, fondi e derivati a fine giornata, all'ultimo prezzo noto in quel giorno.", keywords: "titoli azioni fondi");
        E("*/Obbligazioni", "Obbligazioni", "Valore delle obbligazioni a fine giornata.", keywords: "obbligazioni btp");
        E("*/Crypto", "Crypto", "Valore delle crypto a fine giornata.", keywords: "crypto");
        E("*/Totale", "Totale", "La somma delle quattro colonne precedenti: quanto vale il conto quel giorno.", keywords: "totale somma");
        E("*/Direzione", "Direzione", "Entrata = soldi arrivati sul conto, Uscita = soldi partiti.", keywords: "direzione entrata uscita");
        E("*/Contropartita", "Contropartita", "Chi ha mandato o ricevuto il bonifico, se il broker lo indica.", keywords: "contropartita controparte");
        E("*/IBAN", "IBAN", "L'IBAN dell'altro conto, se indicato.", keywords: "iban conto");
        E("*/Riferimento", "Riferimento", "La causale del bonifico.", keywords: "riferimento causale");
        E("*/Lordo", "Lordo", "Interesse prima delle tasse.", keywords: "lordo");
        E("*/Tassa", "Tassa", "Imposta trattenuta su quell'interesse.", keywords: "tassa imposta");
        E("*/Netto", "Netto", "Lordo meno tassa: quanto è entrato in cassa.", keywords: "netto");
        E("*/Tipo", "Tipo", "Il tipo di operazione: BUY (acquisto), SELL (vendita), e così via.", keywords: "tipo operazione buy sell");
        E("*/Fee", "Fee", "Le commissioni pagate sull'operazione.", keywords: "fee commissioni costi");
        E("*/Tax", "Tax", "Le tasse trattenute sull'operazione.", keywords: "tax tasse");
        E("*/Ultimo prezzo", "Ultimo prezzo",
            "L'ultimo prezzo noto dell'asset, in euro: se hai aggiornato le quotazioni è quello, altrimenti l'ultimo prezzo di un'operazione.",
            keywords: "ultimo prezzo quotazione");
        E("*/Quotazione del", "Quotazione del",
            "Quando è stato rilevato quel prezzo. Se è una data vecchia, il valore dell'asset è una stima poco aggiornata.",
            keywords: "quotazione del data rilevazione prezzo vecchio");
        E("*/Valore stimato", "Valore stimato", "Quantità × ultimo prezzo. Per le posizioni chiuse è zero.", keywords: "valore stimato");
        E("*/Saldo (posizione chiusa)", "Saldo (posizione chiusa)",
            "Per le posizioni chiuse: quanto quell'asset ha dato (o tolto) in cassa in tutta la sua storia.",
            "È la somma di importi, commissioni e tasse di tutte le sue operazioni. Positivo = hai guadagnato, negativo = hai perso.",
            "saldo posizione chiusa guadagno perdita");
        E("securities/Quantità", "Quantità", "Quante unità possiedi oggi. Zero = posizione chiusa.", keywords: "quantita posseduta");
        E("secdetail/Quantità", "Quantità", "Quante unità sono state comprate (+) o vendute (−) in quell'operazione.", keywords: "quantita");
        E("secdetail/Prezzo", "Prezzo", "Il prezzo a cui è stata eseguita l'operazione.", keywords: "prezzo operazione");
        E("gains/Quantità ceduta", "Quantità ceduta", "Quante unità hai venduto (o ti sono scadute) in quella cessione.", keywords: "quantita ceduta venduta");
        E("gains/Ricavo", "Ricavo", "Quello che hai incassato dalla cessione, al netto di commissioni e tasse. Zero per i turbo scaduti.", keywords: "ricavo incassato");
        E("gains/Costo (medio)", "Costo (medio)",
            "Il costo delle unità cedute, al prezzo medio ponderato di tutti i tuoi acquisti di quell'asset.",
            "Costo medio ponderato: ogni acquisto sposta la media, ogni vendita scarica quote a quel costo medio. Non è FIFO.",
            "costo medio ponderato");
        E("gains/Plus/Minus", "Plus/Minus", "Ricavo meno costo: positivo = utile, negativo = perdita.", keywords: "plus minus plusvalenza minusvalenza utile perdita");
        E("asof/Prezzo", "Prezzo", "L'ultimo prezzo noto fino a quel giorno (non oltre).", keywords: "prezzo");
        E("asof/Valore", "Valore", "Per gli asset: quantità × prezzo. Per la liquidità e gli interessi: l'importo.", keywords: "valore");
        E("quotes/N. aggiornamenti", "N. aggiornamenti", "Quante quotazioni hai registrato per quell'asset da quando lo aggiorni.", keywords: "aggiornamenti numero registrate");
        E("quotes/Ultimo aggiornamento", "Ultimo aggiornamento", "Quando è stata presa l'ultima quotazione. Vuoto = mai aggiornato.", keywords: "ultimo aggiornamento");
        E("quotes/Ultimo valore", "Ultimo valore", "L'ultimo prezzo registrato, in euro.", keywords: "ultimo valore prezzo");
        E("quotes/Valuta", "Valuta", "La valuta del prezzo. Le quotazioni in dollari vengono convertite in euro con il cambio del giorno.", keywords: "valuta euro dollari");
        E("quotedetail/Prezzo", "Prezzo", "Il prezzo registrato in quel momento.", keywords: "prezzo");
        E("quotedetail/Fonte", "Fonte", "Da dove arriva il prezzo: coingecko, yahoo o frankfurter.", keywords: "fonte provider origine");
        E("quotedetail/Valuta", "Valuta", "La valuta del prezzo registrato.", keywords: "valuta");

        // ---- Tabella BTP
        E("btp/ISIN", "ISIN", "Il codice internazionale del titolo: lo usi nel broker per cercarlo.", keywords: "isin codice");
        E("btp/Descrizione", "Descrizione", "Il nome del BTP: tasso della cedola, mese e anno di scadenza.", keywords: "descrizione nome");
        E("btp/Cedola annua", "Cedola annua",
            "L'interesse annuo lordo, in percentuale del valore nominale. Si incassa in due rate semestrali.", keywords: "cedola tasso interesse");
        E("btp/Scadenza", "Scadenza", "Quando lo Stato ti rimborsa il valore nominale (100 per ogni 100).", keywords: "scadenza rimborso");
        E("btp/Prezzo", "Prezzo",
            "Il corso secco: il prezzo del titolo senza il rateo, per 100 di nominale. Sotto 100 compri «a sconto», sopra 100 «a premio».",
            "È l'ultimo contratto sul mercato MOT di Borsa Italiana.", "prezzo corso secco");
        E("btp/Anni residui", "Anni residui", "Quanto manca alla scadenza, dalla data di regolamento (oggi + 2 giorni lavorativi).", keywords: "anni residui durata");
        E("btp/Rateo per 100", "Rateo per 100",
            "La cedola già maturata dall'ultimo stacco, per ogni 100 di nominale.",
            "La paghi al venditore quando compri, e la recuperi con la prima cedola: non è un costo vero.", "rateo maturato cedola");
        E("btp/Rendimento cedolare lordo sul capitale investito", "Rendimento cedolare lordo",
            "Quanto rendono le sole cedole rispetto a quanto spendi: 100 × cedola ÷ prezzo.",
            "Non dipende da quanto capitale investi né dalle commissioni. Non conta il guadagno (o la perdita) che avrai a scadenza.",
            "rendimento cedolare lordo");
        E("btp/Rendimento effettivo a scadenza lordo (annuo)", "Rendimento effettivo lordo",
            "Il rendimento annuo se tieni il BTP fino a scadenza, comprese le cedole e la differenza tra prezzo e 100.",
            "È il tasso interno di rendimento con cedole semestrali (la funzione RENDIMENTO di Excel). È il numero più adatto per confrontare BTP diversi.",
            "rendimento effettivo scadenza lordo tir irr");
        E("btp/Rendimento effettivo a scadenza netto (annuo)", "Rendimento effettivo netto",
            "Come il precedente, ma dopo l'imposta sostitutiva sulle cedole e sulla plusvalenza a scadenza.",
            "È un'approssimazione: usa l'aliquota impostata nel calcolatore (di base 12,5%).", "rendimento effettivo scadenza netto tasse");

        // ---- Grafici
        E("chart:donut", "Composizione attuale", "Come si divide il conto oggi tra disponibilità, titoli, obbligazioni e crypto.", keywords: "ciambella torta composizione");
        E("chart:total", "Totale e versamenti netti",
            "La linea blu è il valore del conto; quella tratteggiata sono i soldi che hai versato. Quando la blu è sotto, sei sotto ai versamenti.",
            keywords: "totale versamenti andamento");
        E("chart:trend", "Andamento",
            "Una linea per ogni tipologia più il totale, giorno per giorno. Clicca la legenda per nascondere una linea.", keywords: "andamento linee");
        E("chart:composition", "Composizione e scostamento",
            "Ogni colonna è una data e mostra cosa avevi dentro, per categoria. La linea tratteggiata sono i versamenti netti.",
            "Il triangolino sopra la colonna è verde ▲ se sei sopra ai versamenti, rosso ▼ se sotto. Nel tooltip vedi quanto e la variazione di ogni categoria rispetto alla colonna precedente.",
            "composizione colonne scostamento triangolo");
        E("chart:gap", "Sopra / sotto i versamenti",
            "Colonne mensili: la base blu è il Totale fino al livello dei versamenti, il cappello verde (sopra) o rosso (sotto) è lo scostamento.",
            keywords: "sopra sotto versamenti scostamento cappello");
        E("chart:quote", "Quotazioni registrate", "Come è cambiato il prezzo di questo asset ogni volta che l'hai aggiornato.", keywords: "quotazioni storico prezzo");
        E("tab:trend", "Andamento", "Le linee giornaliere di ogni tipologia.", keywords: "andamento linee");
        E("tab:comp", "Composizione e scostamento", "Le colonne di composizione, con il periodo a tua scelta.", keywords: "composizione");
        E("tab:gap", "Sopra / sotto i versamenti", "Le colonne mensili con il cappello verde o rosso.", keywords: "sopra sotto");
        E("period", "Periodo", "Ogni quanto mettere una colonna: dal giorno al singolo anno. I passi mensili partono dal giorno di oggi.", keywords: "periodo passo intervallo");
        E("period:Daily", "Giornaliero", "Una colonna al giorno (molte: usa la barra in basso per ingrandire).", keywords: "giornaliero");
        E("period:Weekly", "Settimanale", "Una colonna ogni 7 giorni, a ritroso da oggi.", keywords: "settimanale");
        E("period:Monthly", "Mensile", "Una colonna al mese, sempre allo stesso giorno di oggi (o all'ultimo del mese, se non esiste).", keywords: "mensile");
        E("period:Bimonthly", "Bimestrale", "Una colonna ogni 2 mesi.", keywords: "bimestrale");
        E("period:Quarterly", "Trimestrale", "Una colonna ogni 3 mesi.", keywords: "trimestrale");
        E("period:FourMonthly", "Quadrimestrale", "Una colonna ogni 4 mesi.", keywords: "quadrimestrale");
        E("period:SemiAnnual", "Semestrale", "Una colonna ogni 6 mesi.", keywords: "semestrale");
        E("period:Annual", "Annuale", "Una colonna all'anno.", keywords: "annuale");
        E("period:Ytd", "YTD", "Year To Date: due colonne, l'inizio dell'anno e oggi, per vedere come è andato l'anno in corso.", keywords: "ytd anno corso inizio");

        // ---- Tabelle: strumenti comuni
        E("filter", "Filtro", "Scrivi per tenere solo le righe che contengono quel testo in una qualsiasi colonna.", keywords: "filtro cerca ricerca");
        E("export", "Esporta CSV", "Salva la tabella (con filtro e ordine attuali) in un file CSV da aprire con Excel.", keywords: "esporta csv excel salva");
        E("count.rows", "Righe", "Quante righe vedi, e su quante in totale se hai applicato un filtro.", keywords: "righe conteggio");
        E("securities.class", "Filtro per categoria", "Mostra solo azioni, fondi, obbligazioni, crypto o derivati. Il numero accanto è quanti asset ci sono.", keywords: "filtro categoria chip");
        E("securities.open", "Solo posizioni aperte", "Nasconde gli asset che non possiedi più (quantità zero).", keywords: "posizioni aperte chiuse");

        // ---- Composizione a una data
        E("asof.cat.CASH", "Saldo disponibile", "La liquidità che avevi quel giorno.", keywords: "saldo disponibile liquidita");
        E("asof.cat.CRYPTO", "Crypto", "Le crypto possedute quel giorno, con il prezzo noto fino a quella data.", keywords: "crypto");
        E("asof.cat.BOND", "Obbligazioni", "Le obbligazioni possedute quel giorno.", keywords: "obbligazioni");
        E("asof.cat.STOCK", "Azioni", "Le azioni possedute quel giorno.", keywords: "azioni");
        E("asof.cat.FUND", "Fondi", "I fondi/ETF posseduti quel giorno.", keywords: "fondi etf");
        E("asof.cat.DERIVATIVE", "Derivati", "Turbo e warrant posseduti quel giorno.", keywords: "derivati turbo warrant");
        E("asof.cat.INTEREST", "Interessi", "Gli interessi netti incassati fino a quel giorno, uno per riga.", keywords: "interessi");

        // ---- Quotazioni
        E("update.all", "Aggiorna tutto", "Scarica i prezzi di crypto, azioni, fondi e del cambio EUR/USD, fino a 5 richieste in parallelo.", keywords: "aggiorna tutto prezzi scarica");
        E("update.crypto", "Aggiorna Crypto", "Prende i prezzi delle crypto da CoinGecko.", keywords: "aggiorna crypto coingecko");
        E("update.stocks", "Aggiorna Azioni e Fondi", "Prende i prezzi da Yahoo Finance. Le quotazioni in dollari sono convertite in euro.", keywords: "aggiorna azioni fondi yahoo");
        E("update.fx", "Aggiorna Cambio", "Prende il cambio EUR/USD da Frankfurter (dati BCE), usato per convertire i prezzi in dollari.", keywords: "aggiorna cambio eurusd frankfurter");
        E("update.log", "Registro", "Cosa sta succedendo durante l'aggiornamento: verde = riuscito, rosso = fallito (con il motivo).", keywords: "registro log errori");
        E("update.clear", "Pulisci registro", "Cancella le righe del registro: non tocca le quotazioni salvate.", keywords: "pulisci registro");

        // ---- BTP
        E("btp.refresh", "Aggiorna da Borsa Italiana",
            "Scarica l'elenco dei BTP a tasso fisso con l'ultimo prezzo. All'apertura si aggiorna da solo se i dati hanno più di 15 minuti.",
            keywords: "aggiorna borsa italiana scarica elenco");
        E("btp.capital", "Capitale da investire", "Quanti euro vuoi investire: il calcolatore dice quanti tagli ci stanno.", keywords: "capitale investire");
        E("btp.commission", "Commissioni", "Le commissioni di acquisto in euro. Di base zero.", keywords: "commissioni costi");
        E("btp.tax", "Aliquota", "L'imposta su cedole e plusvalenza: di base 12,5% per i titoli di Stato.", keywords: "aliquota imposta tasse");
        E("btp.lot", "Taglio minimo", "L'importo minimo acquistabile: di base 1.000 € di nominale.", keywords: "taglio minimo lotto");
        E("btp.settlement", "Regolamento", "La data in cui paghi e ricevi il titolo: di base oggi + 2 giorni lavorativi (T+2). Cambia rateo e rendimenti.", keywords: "regolamento data t2");
        E("btp.summary", "Riepilogo", "La sintesi in una frase: quanto nominale compri, cedola annua incassata e surplus a scadenza.", keywords: "riepilogo sintesi");
        E("btp.col.market", "Prezzo di mercato", "I numeri dell'acquisto al prezzo reale di oggi.", keywords: "prezzo mercato");
        E("btp.col.at100", "Se quotasse 100", "Gli stessi numeri come se il titolo costasse 100: serve a vedere quanto vale comprare sotto (o sopra) la pari.", keywords: "quotasse 100 pari");
        E("btp.col.diff", "Differenza", "Mercato meno «se quotasse 100»: positivo = ci guadagni rispetto al prezzo 100.", keywords: "differenza");
        E("btp.row.price", "Prezzo secco", "Il prezzo senza rateo, per 100 di nominale.", keywords: "prezzo secco");
        E("btp.telquel", "Prezzo tel quel", "Prezzo secco + rateo maturato: quanto paghi davvero per 100 di nominale quando compri.", "Si chiama «tel quel» perché il prezzo comprende gli interessi già maturati. Il rateo non è un costo: lo recuperi con la prima cedola.", "tel quel telquel prezzo rateo");
        E("btp.row.lotcost", "Costo di un taglio", "Prezzo + rateo, applicati al lotto minimo: quanto paghi davvero per un taglio («tel quel»).", keywords: "costo taglio tel quel");
        E("btp.row.lots", "Numero di tagli", "Quanti tagli interi ci stanno nel capitale, tolte le commissioni.", keywords: "tagli numero lotti");
        E("btp.row.nominal", "Valore nominale", "L'importo che lo Stato ti restituisce a scadenza: tagli × taglio minimo.", keywords: "valore nominale rimborso");
        E("btp.row.clean", "Controvalore al prezzo secco", "Nominale × prezzo ÷ 100: quanto vale l'acquisto senza rateo.", keywords: "controvalore prezzo secco");
        E("btp.row.accrued", "Rateo pagato", "La cedola maturata che paghi al venditore. La recuperi interamente con la prima cedola.", keywords: "rateo pagato venditore");
        E("btp.row.outlay", "Esborso totale", "Controvalore + rateo + commissioni: i soldi che escono davvero dal conto.", keywords: "esborso totale spesa");
        E("btp.row.leftover", "Liquidità che avanza", "Capitale meno esborso: il resto che non basta per un altro taglio.", keywords: "liquidita avanza resto");
        E("btp.row.coupon", "Cedola", "La cedola lorda incassata: semestrale (metà) o annua (intera), prima dell'imposta.", keywords: "cedola semestrale annua lorda");
        E("btp.row.couponnet", "Cedola netta", "La cedola annua dopo l'imposta.", keywords: "cedola netta");
        E("btp.row.surplus", "Surplus a scadenza",
            "Rimborso meno prezzo secco: positivo = guadagni in conto capitale (hai comprato sotto 100), negativo = perdi (hai comprato sopra 100).",
            "Se è negativo genera una minusvalenza che si può compensare: l'app non la calcola.", "surplus scadenza capital gain");
        E("btp.row.surplustax", "Imposta sul surplus", "L'aliquota applicata solo se il surplus è positivo.", keywords: "imposta surplus");
        E("btp.row.gain", "Guadagno totale", "Cedole + rimborso − esborso, fino a scadenza. Il netto toglie le imposte (stima semplificata).", keywords: "guadagno totale lordo netto");
        E("btp.row.yield", "Rendimento effettivo", "Il tasso annuo che tiene insieme cedole, prezzo e rimborso. Per confrontare i BTP è il numero più utile.", keywords: "rendimento effettivo tir");
        E("btp.row.couponyield", "Rendimento cedolare", "Cedola annua ÷ controvalore secco. A prezzo 100 coincide con il tasso della cedola.", keywords: "rendimento cedolare");
        E("btp.row.surplusann", "Surplus annualizzato", "Quanto il recupero verso 100 aggiunge ogni anno, in percentuale del capitale investito.", keywords: "surplus annualizzato");
        E("btp.row.totalcoupons", "Totale cedole", "Tutte le cedole lorde che incasserai fino a scadenza.", keywords: "totale cedole");

        // ---- Dati e file
        E("files.folder", "Cartella dati", "La cartella principale dove l'app salva tutto quello che non è nel CSV: {datafolder}.", keywords: "cartella dati percorso appdata localappdata");
        E("files.open", "Apri cartella", "Apre la cartella in Esplora file.", keywords: "apri cartella esplora");
        E("files.copy", "Copia percorso", "Copia negli appunti il percorso della cartella.", keywords: "copia percorso appunti");
        E("files.backup", "Esporta backup", "Crea un file zip con quotazioni, catalogo, BTP e preferenze. Non include i file tecnici dell'interfaccia.", keywords: "backup esporta zip copia sicurezza");
        E("files.csv", "Il tuo CSV", "Il file delle operazioni caricato adesso: {filename}. L'app non lo modifica mai.", keywords: "csv file operazioni percorso");
        E("files.quotes", "File di quotazione", "Un file JSON per ogni asset, con tutte le quotazioni registrate. Aggiornare un asset non tocca gli altri.", keywords: "quotazione quotazioni json file cartella");
        E("file:quotes", "quotes\\", "Una cartella con un file JSON per asset: lo storico delle quotazioni.", keywords: "quotes quotazioni storico");
        E("file:catalog.json", "catalog.json", "L'elenco degli asset che aggiorni, con la fonte di prezzo e il nome del file di ognuno.", keywords: "catalogo asset");
        E("file:btp.json", "btp.json", "L'ultimo elenco BTP scaricato e gli ultimi valori usati nel calcolatore.", keywords: "btp elenco calcolatore");
        E("file:lastfile.txt", "lastfile.txt", "Il percorso dell'ultimo CSV aperto, per riaprirlo all'avvio.", keywords: "ultimo file csv");
        E("file:webprefs.json", "webprefs.json", "Le tue preferenze: tema, ordinamento delle tabelle, filtri, modalità assistita.", keywords: "preferenze tema ordinamento");
        E("file:uisettings.json", "uisettings.json", "Posizione e colonne delle finestre della vecchia interfaccia, non più usate.", keywords: "impostazioni vecchia classica");
        E("file:www", "www\\", "I file dell'interfaccia (pagine, grafici, icone), copiati dall'eseguibile a ogni avvio. Puoi ignorarla.", keywords: "interfaccia www tecnici");
        E("file:webview2", "webview2\\", "I dati interni del browser incorporato (cache). Puoi ignorarli.", keywords: "webview2 cache browser tecnici");
        E("files/File", "File", "Il nome del file o della cartella.", keywords: "file nome");
        E("files/Contenuto", "Contenuto", "A cosa serve quel file.", keywords: "contenuto");
        E("files/Dimensione", "Dimensione", "Quanto spazio occupa.", keywords: "dimensione spazio");
        E("files/Modificato", "Modificato", "L'ultima volta che il file è stato scritto.", keywords: "modificato data");
        E("filesq/File", "File", "Il nome del file JSON in cui sono salvate le quotazioni di quell'asset.", keywords: "file nome json");
        E("filesq/Asset", "Asset", "L'asset a cui appartiene quel file.", keywords: "asset");
        E("filesq/Quotazioni", "Quotazioni", "Quante quotazioni contiene il file.", keywords: "quotazioni numero");

        // ---- Versione 2.2: pagine nuove
        E("page:/returns", "Rendimenti",
            "Quanto ha reso davvero il conto, tenendo conto di quando hai versato i soldi: rendimento ponderato nel tempo (TWR), annuo sui tuoi soldi (XIRR) e guadagno in euro.",
            "Il solo confronto «Totale − versamenti» non dice se hai reso bene: mille euro versati ieri non hanno avuto il tempo di rendere quanto mille versati un anno fa. Qui trovi i due modi standard di misurarlo.");
        E("page:/costs", "Costi e tasse", "Quanto hai pagato in commissioni e imposte, per mese e per anno.",
            "Sono le somme delle colonne «fee» e «tax» del tuo file, divise per tipo di operazione. Non sono un calcolo fiscale.");
        E("page:/coupons", "Cedole e scadenze", "Quando incassi cedole e rimborsi dei BTP che possiedi.",
            "Serve l'elenco dei BTP (cedola e scadenza): si scarica dalla pagina BTP. Di turbo e derivati l'esportazione non indica la scadenza.");
        E("page:/checks", "Controlli", "Verifiche automatiche sui tuoi dati, per accorgersi di numeri che non possono essere giusti.",
            "Ogni verifica mostra «ok» o cosa non torna. Il numero giallo accanto a «Controlli» nel menu dice quante segnalazioni ci sono.");
        E("page:/btp/compare", "Confronto BTP", "Gli stessi soldi investiti in due o tre BTP, affiancati, al prezzo di mercato di oggi.",
            "In verde il valore migliore di ogni riga: il più alto per cedole, guadagni e rendimenti, il più basso per l'esborso.");
        E("nav:/returns", "Rendimenti", "Rendimento ponderato nel tempo, annuo e guadagno in euro.", keywords: "rendimenti twr xirr guadagno");
        E("nav:/costs", "Costi e tasse", "Commissioni e imposte pagate.", keywords: "costi commissioni imposte tasse");
        E("nav:/coupons", "Cedole e scadenze", "Il calendario degli incassi dei BTP posseduti.", keywords: "cedole scadenze rimborsi calendario");
        E("nav:/checks", "Controlli", "Verifiche automatiche sui dati. Il numero giallo indica quante segnalazioni ci sono.", keywords: "controlli verifiche coerenza");
        E("nav:search", "Cerca", "Apre la ricerca rapida: scrivi il nome di una pagina, di un titolo o di un BTP, oppure un comando. Scorciatoia: Ctrl+K.", keywords: "cerca ricerca rapida ctrl k");
        E("nav:privacy", "Privacy", "Nasconde (sfoca) tutti gli importi e i grafici, utile quando condividi lo schermo o registri un video. Scorciatoia: F2.", keywords: "privacy nascondi importi sfoca schermo");
        E("banner.checks", "Segnalazioni", "Ci sono dei controlli sui dati che non tornano: clicca per vedere quali.", keywords: "segnalazioni avviso controlli");
        E("banner.newfile", "Nuovo file trovato", "Nella cartella del file aperto è comparso un CSV più recente con lo stesso formato. Puoi aprirlo al posto di quello attuale o ignorarlo. Se usi lo storico delle operazioni, puoi invece aggiungere i suoi movimenti.", keywords: "nuovo file csv cartella");
        E("export.pdf", "Esporta PDF", "Salva questa pagina in un PDF, senza menu né pulsanti.", keywords: "esporta pdf stampa");
        E("export.xlsx", "Esporta Excel", "Salva la tabella (con filtro e ordine attuali) in un file Excel vero: numeri, importi, percentuali e date si possono sommare e ordinare.", keywords: "esporta excel xlsx");
        E("btp.compare", "Confronta BTP", "Attiva la scelta di 2 o 3 titoli: poi «Confronta» li mette affiancati con lo stesso capitale.", keywords: "confronta btp confronto");
        E("update.auto", "Aggiorna da sola all'apertura", "Se i prezzi hanno più di 12 ore, all'apertura dell'app vengono aggiornati da soli. Puoi spegnerlo qui.", keywords: "automatico aggiornamento apertura");
        E("files.autobackup", "Backup automatico", "Una copia dei dati ogni 7 giorni nella cartella scelta (ne tiene 8). Non include il tuo CSV.", keywords: "backup automatico copia settimanale");
        E("goal.card", "Obiettivo", "Una cifra che vuoi raggiungere: qui vedi a che punto sei, e compare come linea tratteggiata nei grafici.", keywords: "obiettivo traguardo target");

        // ---- Versione 2.2: riquadri e colonne nuove
        E("unrealized.card", "Plus/minus latente",
            "Quanto guadagneresti (o perderesti) se vendessi oggi tutte le posizioni aperte: valore attuale meno quanto ti sono costate. Clicca per vederle.",
            "Il costo è il costo medio ponderato di quanto possiedi ancora (commissioni comprese). Non è realizzata finché non vendi.", "plus minus latente non realizzata posizioni aperte");
        E("twr.card", "Rendimento (TWR)", "Come è andata la gestione, a prescindere da quando hai versato. Clicca per i dettagli.",
            "Concatena i rendimenti di ogni giorno togliendo l'effetto di versamenti e prelievi (metodo di Dietz: i bonifici contano a metà giornata).", "twr rendimento ponderato nel tempo");
        E("xirr.card", "Rendimento annuo (XIRR)", "Il tasso annuo che hanno reso i tuoi soldi, pesando ogni versamento per il tempo in cui è rimasto investito. Clicca per i dettagli.",
            "È il tasso interno di rendimento con le date dei bonifici, come la funzione XIRR di Excel. Su periodi sotto l'anno è poco significativo.", "xirr rendimento annuo tasso interno");
        E("costs.card", "Costi e tasse pagati", "Commissioni più imposte nette pagate in tutto. Clicca per vedere come si dividono.", keywords: "costi commissioni tasse pagati");
        E("concentration.card", "Concentrazione", "Quanta parte del conto sta nelle 5 posizioni più grandi: più è alta, più dipendi da poche scelte.", keywords: "concentrazione peso prime posizioni diversificazione");
        E("returns.gain", "Guadagno in euro", "Valore del conto meno quanto ci hai versato: il numero più semplice, senza ipotesi.", keywords: "guadagno euro");
        E("returns.years", "Anno per anno", "Valore iniziale e finale, versamenti, guadagno, TWR e XIRR di ogni anno solare.", keywords: "anno per anno annuale rendimenti");
        E("returns.categories", "Per categoria", "Il rendimento ponderato nel tempo di titoli, obbligazioni e crypto, tenendo acquisti e vendite come flussi.", keywords: "categoria rendimenti titoli obbligazioni crypto");
        E("returns.howto", "Come leggerli", "TWR = come è andata la gestione; XIRR = come sono andati i tuoi soldi; guadagno = quanto vale il conto meno quanto ci hai messo.", keywords: "come leggere rendimenti");
        E("*/TWR", "TWR", "Rendimento ponderato nel tempo del periodo: non dipende da quando hai versato.", keywords: "twr rendimento ponderato tempo");
        E("*/XIRR", "XIRR annuo", "Rendimento annuo dei tuoi soldi, tenendo conto delle date dei versamenti. Con un asterisco: periodo sotto l'anno, poco significativo.", keywords: "xirr annuo rendimento");
        E("*/Plus/minus latente", "Plus/minus latente", "Valore attuale della posizione meno il suo costo medio ponderato: guadagno (o perdita) non ancora realizzato.", keywords: "plus minus latente non realizzata");
        E("*/Plus/minus latente %", "Plus/minus latente %", "La plus/minus latente in percentuale del costo.", keywords: "plus minus latente percentuale");
        E("*/Costo medio", "Costo medio", "Quanto ti è costata in media ogni unità che possiedi ancora (commissioni comprese), a costo medio ponderato.", keywords: "costo medio prezzo medio carico");
        E("*/Costo totale", "Costo totale", "Quanto ti è costata la quantità che possiedi ancora: costo medio × quantità.", keywords: "costo totale carico");
        E("*/Peso", "Peso", "Quanto vale la posizione rispetto al totale del conto.", keywords: "peso percentuale conto");
        E("*/Imposte su compravendite", "Imposte su compravendite", "Imposte trattenute dal broker sugli acquisti e sulle vendite (colonna «tax» di BUY e SELL).", keywords: "imposte compravendite");
        E("*/Imposte sugli interessi", "Imposte sugli interessi", "L'imposta trattenuta sugli interessi della liquidità.", keywords: "imposte interessi");
        E("*/Bollo e ricalcoli", "Bollo e ricalcoli", "Imposta di bollo e ricalcoli fiscali (righe «TAX» del file).", keywords: "bollo ricalcoli tax");
        E("*/Ottimizzazione fiscale", "Ottimizzazione fiscale", "Rimborsi o compensazioni fiscali (righe «TAX_OPTIMIZATION»): per questo possono essere negative.", keywords: "ottimizzazione fiscale rimborsi");
        E("costs.total", "Costi e tasse in tutto", "Commissioni più imposte nette pagate dall'inizio. Sotto: a quanto equivalgono rispetto ai versamenti.", keywords: "costi totale");
        E("costs.fees", "Commissioni", "La somma delle commissioni di tutte le operazioni.", keywords: "commissioni");
        E("costs.tax", "Imposte nette", "Imposte pagate meno quelle rimborsate.", keywords: "imposte nette");
        E("costs.perTrade", "Commissione media", "Commissioni totali divise per il numero di compravendite.", keywords: "commissione media operazione");
        E("costs.years", "Anno per anno", "Costi e imposte divisi per tipo, un anno per riga.", keywords: "anno costi imposte");
        E("costs.howto", "Cosa c'è dentro", "Cosa significa ogni colonna dei costi e perché i rimborsi appaiono in negativo.", keywords: "costi spiegazione");
        E("chart:twr", "Rendimento cumulato", "Il rendimento ponderato nel tempo giorno dopo giorno: parte da zero e mostra quanto sei salito o sceso.", keywords: "rendimento cumulato twr grafico");
        E("chart:costs", "Costi mese per mese", "Commissioni (blu) e imposte nette (giallo) di ogni mese.", keywords: "costi mensili grafico");
        E("chart:coupons", "Incassi mese per mese", "Cedole nette (verde) e rimborsi (blu) dei prossimi 24 mesi.", keywords: "incassi cedole rimborsi grafico");
        E("coupons.next", "Prossimo incasso", "La prima cedola o il primo rimborso in arrivo, al netto dell'imposta.", keywords: "prossimo incasso");
        E("coupons.year", "Cedole nei prossimi 12 mesi", "Quanto incassi di cedole nell'anno che viene, al netto (e al lordo) dell'imposta.", keywords: "cedole 12 mesi");
        E("coupons.redemption", "Rimborsi a scadenza", "Il valore nominale che lo Stato ti restituirà a scadenza dei BTP che possiedi.", keywords: "rimborsi scadenza nominale");
        // ---- Versione 2.3: Valute
        E("page:/currencies", "Valute",
            "I cambi principali contro l'euro, aggiornati quasi in tempo reale, con l'andamento degli ultimi giorni, un convertitore e quanta parte del tuo conto è in valute diverse dall'euro.",
            "La fonte principale è Yahoo Finance (aggiornata al secondo, ma non ufficiale). Se non risponde si usano i cambi di riferimento della BCE, che escono una volta al giorno. Lo storico dei grafici è sempre BCE.");
        // ---- Versione 2.4: posizioni chiuse
        E("page:/closed", "Posizioni chiuse", "I titoli su cui hai venduto tutta la quantità, con il risultato di ciascuno dal primo acquisto all'ultima vendita. Se ne hai ricomprato qualcuno, contano solo i cicli già chiusi.",
            "Si parla di «ciclo» da quando compri (la quantità sale da zero) a quando la quantità torna a zero. Se ricompri più avanti è un nuovo ciclo: nel dettaglio del titolo li vedi uno per uno e il totale nel tempo. Gli importi sono netti di commissioni e imposte già trattenute dal broker.");
        E("nav:/closed", "Posizioni chiuse", "I titoli su cui hai chiuso tutta la posizione, con il risultato di ognuno.", keywords: "posizioni chiuse titoli venduti risultato ciclo knock out turbo");
        E("closed.filters", "Filtri", "Mostra solo alcune categorie, e scegli un anno di chiusura. Le schede e il grafico si aggiornano con i filtri.", keywords: "filtri categoria anno");
        E("closed.year", "Anno di chiusura", "Mostra solo le posizioni chiuse in quell'anno (conta la data dell'ultima vendita).", keywords: "anno chiusura");
        E("closed.group", "Raggruppa per sottostante", "Unisce i prodotti dello stesso sottostante (per esempio tutti i turbo su una stessa azione, che hanno nomi quasi uguali) in una sola riga, con i totali.", keywords: "raggruppa sottostante turbo");
        E("closed.count", "Posizioni chiuse", "Quanti titoli hai chiuso (con i filtri scelti) e quante operazioni ci sono dietro.", keywords: "posizioni chiuse numero");
        E("closed.result", "Risultato totale", "La somma dei risultati delle posizioni chiuse: quanto è entrato dalle vendite meno quanto è uscito per gli acquisti, già al netto di commissioni e imposte trattenute.", keywords: "risultato totale netto");
        E("closed.winners", "In guadagno", "Quante posizioni hai chiuso in positivo, la loro percentuale sul totale e il guadagno medio.", keywords: "guadagno vincenti percentuale media");
        E("closed.losers", "In perdita", "Quante posizioni hai chiuso in negativo, la loro percentuale sul totale e la perdita media.", keywords: "perdita perdenti percentuale media");
        E("closed.paid", "Costi e imposte pagati", "Le commissioni e le imposte che il broker ha già trattenuto su queste posizioni.", keywords: "costi imposte commissioni pagati");
        E("closed.extremes", "Migliore e peggiore", "La posizione chiusa con il risultato più alto e quella con il più basso.", keywords: "migliore peggiore massimo minimo");
        E("closed.chart", "Risultato per anno di chiusura", "I guadagni (verde) e le perdite (rosso) delle posizioni chiuse, anno per anno.", keywords: "grafico risultato anno");
        E("closed.years", "Anno per anno", "Per ogni anno di chiusura: quante posizioni, quante in guadagno e in perdita, il risultato, le commissioni e le imposte già pagate.", keywords: "anno per anno riepilogo");
        E("closed.taxes", "Imposte già pagate", "Le imposte che il broker ha trattenuto sulle vendite di quelle posizioni (colonna «tax» del file). Non sono un calcolo fiscale: sono quanto risulta già pagato.", keywords: "imposte pagate trattenute tax");
        E("sec.cycles", "Cicli di questa posizione", "Ogni volta che la quantità sale da zero e poi torna a zero è un ciclo. Qui vedi il risultato di ciascuno e, se sono più di uno, quanto hai fatto in tutto su questo titolo.", keywords: "cicli posizione risultato nel tempo");
        E("closed/Giorni", "Giorni", "Da quanti giorni durava la posizione: dal primo acquisto all'ultima vendita.", keywords: "giorni durata");
        E("closed/Cicli", "Cicli", "Quante volte hai aperto e richiuso quella posizione. Con più di un ciclo il risultato è la somma di tutti.", keywords: "cicli");
        E("closed/Investito", "Investito", "Quanto è uscito dal conto per acquistare (commissioni comprese).", keywords: "investito acquisti");
        E("closed/Incassato", "Incassato", "Quanto è entrato dalle vendite, al netto di commissioni e imposte trattenute.", keywords: "incassato vendite");
        E("closed/Imposte pagate", "Imposte pagate", "Le imposte già trattenute dal broker su quelle vendite.", keywords: "imposte pagate");
        E("closed/Risultato", "Risultato", "Incassato meno investito: quanto hai guadagnato (o perso) sulla posizione.", keywords: "risultato guadagno perdita");
        E("closed/Rendimento", "Rendimento", "Il risultato in percentuale di quanto hai investito. Vuoto se non hai pagato nulla (per esempio crypto ricevute in regalo).", keywords: "rendimento percentuale");
        E("closed/Stato", "Stato", "«Chiusa» se hai venduto tutto; «Più cicli» se l'hai aperta e richiusa più volte; «Riaperta» se l'hai ricomprata e ce l'hai ancora (contano solo i cicli già chiusi); «Knock-out / scaduto» per i turbo che sono finiti a zero senza una vendita registrata (li considero chiusi a perdita).", keywords: "stato knock out scaduto cicli");

        // ---- Versione 2.4: zainetto fiscale
        E("page:/tax", "Zainetto fiscale", "Anno per anno, cosa potresti dover dichiarare divisa per tipo di reddito, con le imposte che il broker ha già trattenuto e lo zainetto delle minusvalenze da riportare.",
            "È un prospetto informativo calcolato dai tuoi movimenti, non un modello da riportare in dichiarazione. Il regime (amministrato o dichiarativo) non cambia i numeri: cambia solo chi li dichiara. Fai sempre verificare tutto da un commercialista.");
        E("nav:/tax", "Zainetto fiscale", "Anno per anno cosa potresti dichiarare, per tipo di reddito, e le minusvalenze da riportare.", keywords: "zainetto fiscale tasse dichiarazione redditi diversi minusvalenze compensazione cripto");
        E("tax.disclaimer", "Solo a scopo informativo", "Gli importi sono stime ricavate dal file del broker: non sono consulenza fiscale e non sono pronti per la dichiarazione. Le regole cambiano e il file non dice tutto.", keywords: "avviso informativo consulenza commercialista");
        E("tax.years", "Anno per anno", "Per ogni anno il risultato delle cessioni (guadagni meno perdite, commissioni comprese) diviso per tipo di strumento, gli interessi incassati, le imposte che il broker ha già trattenuto e il valore del conto a fine anno. Clicca un anno per vederne il dettaglio.", keywords: "anno per anno risultato cessioni categorie");
        E("tax.col.interest", "Interessi e cedole (lordi)", "Gli interessi sulla liquidità e le cedole delle obbligazioni incassati nell'anno, prima delle imposte.", keywords: "interessi cedole lordi");
        E("tax.col.paid", "Imposte già trattenute", "Tutte le imposte che il broker ha già trattenuto nell'anno (colonna «tax» del file): sulle vendite, sugli interessi, imposta di bollo e ricalcoli, al netto dei rimborsi. Sono già state pagate: non vanno pagate di nuovo.", keywords: "imposte trattenute pagate tax bollo");
        E("tax.col.value", "Valore a fine anno", "Il valore di titoli, obbligazioni e crypto al 31 dicembre (per l'anno in corso, all'ultimo giorno disponibile). Utile per i controlli sulle attività finanziarie detenute all'estero.", keywords: "valore fine anno patrimonio estero");
        E("tax.backpack", "Zainetto delle minusvalenze", "Se in un anno perdi più di quanto guadagni, la differenza entra nello «zainetto» e puoi usarla per compensare i guadagni dei quattro anni successivi, partendo dalla più vecchia.",
            "Qui l'ho calcolato per i «redditi diversi» (azioni, ETC/ETN, obbligazioni, derivati e certificati). Le plusvalenze di ETF e fondi sono redditi di capitale e non si compensano; le cripto-attività hanno uno zainetto a parte.", "zainetto minusvalenze riporto compensazione quattro anni");
        E("tax.year", "Dettaglio dell'anno", "Scegli un anno per vedere i redditi divisi per tipo, le imposte già pagate per ogni strumento e ogni singola vendita con il costo abbinato.", keywords: "dettaglio anno vendite strumenti");
        E("tax.card.diversi", "Redditi diversi", "Il risultato netto delle cessioni dell'anno di azioni, ETC/ETN, obbligazioni, derivati e certificati (più le minusvalenze degli ETF). Sotto trovi quanto resta da tassare dopo aver usato le minusvalenze dello zainetto.", keywords: "redditi diversi plusvalenze minusvalenze");
        E("tax.card.capitale", "Redditi di capitale", "Le plusvalenze di ETF e fondi (OICR) e gli interessi e le cedole: non si compensano con le minusvalenze.", keywords: "redditi capitale etf oicr interessi");
        E("tax.card.cripto", "Cripto-attività", "Il risultato netto delle cessioni di crypto dell'anno. Le minusvalenze cripto hanno uno zainetto a parte.", keywords: "cripto crypto plusvalenze");
        E("tax.card.paid", "Imposte già trattenute", "Le imposte che il broker ha già trattenuto nell'anno su vendite e interessi, come risulta dal file.", keywords: "imposte trattenute pagate");
        E("tax.funds", "Come trattare i fondi", "Il file non dice se un fondo è un ETF/OICR o un ETC/ETN, che hanno regole diverse. L'app lo deduce dal nome: qui puoi correggere la scelta per ogni titolo.", keywords: "fondi etf etc etn oicr trattamento classificazione");
        E("tax.assumptions", "Come sono calcolati i numeri", "Le ipotesi e i limiti del prospetto: metodo di calcolo del costo, commissioni, ETF, zainetto, turbo, aliquote.", keywords: "ipotesi limiti metodo lifo costo medio");
        E("taxinst/Trattamento", "Trattamento", "In quale tipo di reddito ricade lo strumento: cambia il modo in cui si compensano guadagni e perdite.", keywords: "trattamento categoria reddito");
        E("taxinst/Costo abbinato", "Costo abbinato", "Il costo degli acquisti che corrispondono alle quote vendute (LIFO, o costo medio per ETF/fondi e crypto), commissioni comprese.", keywords: "costo abbinato lifo");
        E("taxinst/Imposte già pagate", "Imposte già pagate", "Le imposte che il broker ha già trattenuto sulle vendite di questo strumento in quell'anno.", keywords: "imposte pagate trattenute");
        E("tax.checks", "Riscontri sui numeri", "Controlli sul prospetto: confrontano le imposte del file con quelle del prospetto e segnalano ciò che lo rende meno preciso (costi mancanti, turbo azzerati senza vendita, fondi classificati dal nome, fusioni e storni, cripto ricevute gratis).",
            "Non sono un calcolo fiscale: servono a capire quanto fidarsi dei numeri. Un avviso significa che qualcosa può falsare il risultato; un'informazione, che il prospetto ha fatto una scelta che conviene conoscere.", "riscontri controlli imposte prospetto affidabilità avvisi");
        E("tax.missing", "Costo mancante", "Hai venduto più quote di quelle che risultano acquistate nel file (per esempio titoli arrivati da un altro broker o uno storico incompleto). Per quelle quote il costo non c'è e conta come zero: il guadagno mostrato è troppo alto finché non aggiungi gli acquisti mancanti.", keywords: "costo mancante acquisti quote vendute avviso");
        E("taxsales/Quote senza acquisto", "Quote senza acquisto", "Le quote di questa vendita per cui nel file non c'è un acquisto: il loro costo conta come zero.", keywords: "quote senza acquisto costo mancante");
        E("taxsales/Trattamento", "Trattamento", "In quale tipo di reddito ricade lo strumento: cambia il modo in cui si compensano guadagni e perdite.", keywords: "trattamento categoria reddito");
        E("taxsales/Costo abbinato", "Costo abbinato", "Il costo degli acquisti che corrispondono alle quote di questa vendita (LIFO, o costo medio per ETF/fondi e crypto), commissioni comprese.", keywords: "costo abbinato lifo");
        E("taxsales/Imposta già pagata", "Imposta già pagata", "L'imposta che il broker ha già trattenuto su questa vendita.", keywords: "imposta pagata trattenuta");

        // ---- Versione 2.9: mappa del portafoglio e slider del tempo
        E("page:/heatmap", "Mappa del portafoglio",
            "Ogni riquadro è un titolo: più è grande, più vale. Il colore dice quanto stai guadagnando o perdendo rispetto a quanto l'hai pagato. Con lo slider vedi il portafoglio in un giorno del passato.",
            "I titoli sono raggruppati per categoria. Il colore va dal rosso al verde ed è al massimo a ±50% sul costo (i turbo si muovono di più, ma oltre quel punto il colore non cambia). I riquadri grigi sono titoli di cui non si conosce il costo. Il giorno scelto con lo slider vale anche per «Composizione a una data».");
        E("nav:/heatmap", "Mappa del portafoglio", "I tuoi titoli come riquadri colorati: grandezza = valore, colore = guadagno sul costo. Con lo slider puoi tornare indietro nel tempo.", keywords: "mappa heat map heatmap titoli portafoglio colori guadagno perdita slider tempo");
        E("chart:heatmap", "Mappa del portafoglio", "Un riquadro per titolo: la grandezza è il valore, il colore è il guadagno o la perdita rispetto al costo. Passa sopra un riquadro per i numeri, cliccalo per aprire i movimenti del titolo.", keywords: "mappa riquadri titoli colori");
        E("heatmap.legend", "Scala dei colori", "Verde = in guadagno, rosso = in perdita, rispetto a quanto hai pagato quel titolo (costo medio). Il colore è al massimo a ±50%. Grigio = costo non noto.", keywords: "scala colori legenda");
        E("heatmap.value", "Valore dei titoli", "Quanto valevano, in tutto, i titoli rappresentati nella mappa nel giorno scelto.", keywords: "valore titoli mappa");
        E("heatmap.pnl", "Plus/minus latente", "Guadagno o perdita complessivi, non ancora incassati, sui titoli di cui si conosce il costo, e in percentuale del costo.", keywords: "plus minus latente guadagno perdita");
        E("heatmap.count", "Titoli in portafoglio", "Quanti titoli compaiono nella mappa nel giorno scelto.", keywords: "titoli numero");
        E("slider.date", "Slider del tempo", "Trascina il cursore per scegliere il giorno, dal primo dei tuoi dati a oggi; con le frecce della tastiera ti sposti di un giorno alla volta. Lo stesso giorno vale per la Mappa del portafoglio e per «Composizione a una data».", keywords: "slider tempo data giorno cursore passato frecce");
        E("slider.play", "Riproduci", "Fa scorrere i giorni da soli, in avanti, fino a oggi. Premi di nuovo per fermarti; a fine corsa si riparte dall'inizio.", keywords: "riproduci play animazione tempo");
        E("slider.speed", "Velocità della riproduzione", "Cambia la velocità: ×1 (un passo al secondo, circa un minuto e mezzo per tutto il periodo), ×2 o ×4. Si ricorda anche alla prossima apertura.", keywords: "velocità riproduzione");

        // ---- Versione 2.8: emittente e nome dell'emittente
        E("*/Emittente", "Emittente", "Chi ha emesso il derivato (Vontobel o Société Générale). Lo si capisce dal prefisso del codice ISIN, quindi c'è anche per i prodotti già chiusi. Per azioni, fondi e il resto la colonna resta vuota.",
            "Può servire per vedere quanta parte dei tuoi derivati dipende da un solo emittente: se l'emittente fallisse, il valore del prodotto andrebbe perso anche con il sottostante in salita.", "emittente vontobel societe generale derivati turbo");
        E("names/Nome dell'emittente", "Nome dell'emittente", "Il nome del prodotto come lo scrive l'emittente sul suo sito (per esempio «Turbo su Oro»), con in coda l'etichetta del file («Long 4.100 $») che distingue un prodotto dall'altro. Lo si impara quando si leggono i prezzi dei derivati.",
            "Nelle liste questo nome prende il posto di quello ricavato dal file del broker. Se hai scelto un tuo nome, vince il tuo.", "nome emittente derivati prodotto");

        // ---- Versione 2.7: prezzi dei derivati
        E("update.derivatives", "Derivati", "Legge dai siti degli emittenti che hai attivato (Vontobel e Société Générale) il prezzo (Geld e Brief) dei tuoi derivati aperti, uno alla volta e con una pausa tra una richiesta e l'altra.", keywords: "derivati turbo vontobel societe generale prezzo geld brief aggiorna");
        E("update.vontobel", "Prezzi dei derivati da Vontobel", "Con la spunta l'app legge dal sito di Vontobel il prezzo dei derivati aperti (codici DE000V… e DE000BD…) all'apertura e quando aggiorni. Al sito si manda solo il codice del prodotto. Il sito dice di essere per uso privato non commerciale e rivolto a chi risiede in Germania, Austria, Lussemburgo e Liechtenstein; i prezzi possono essere in ritardo. Puoi toglierla quando vuoi.",
            "Come valore si usa il Geld, il prezzo a cui potresti vendere. I derivati di Société Générale si attivano a parte, con la loro spunta.", "vontobel derivati turbo prezzi attiva spegni condizioni");
        E("update.socgen", "Prezzi dei derivati da Société Générale", "Con la spunta l'app legge dal sito di Société Générale il prezzo dei derivati aperti (codici DE000F… e DE000S…) all'apertura e quando aggiorni. Al sito si manda solo il codice del prodotto. Puoi toglierla quando vuoi.",
            "Come valore si usa il Geld, il prezzo a cui potresti vendere. Le Note Legali del sito vietano la riproduzione dei contenuti con qualsiasi mezzo, memorizzazione compresa, senza autorizzazione scritta: attivandola, usi la funzione sotto la tua responsabilità. Il sito è per chi risiede in Italia e chiede di accettare una dichiarazione che qui non compare.", "societe generale derivati turbo prezzi attiva spegni condizioni note legali");

        // ---- Versione 2.6: nomi dei titoli
        E("page:/names", "Nomi dei titoli", "I nomi dei titoli: quelli che hai scelto tu al posto di quelli del file e, per i derivati, quelli letti dal sito dell'emittente. Cliccando un codice (ISIN o ticker), in qualsiasi pagina, puoi cambiarne il nome: da quel momento quel codice si chiama così in tutto il programma.",
            "I tuoi nomi si salvano nel file nomi-titoli.json, quelli degli emittenti in nomi-emittenti.json, nella cartella dati, e finiscono nel backup. I tuoi CSV non vengono mai modificati: il nome serve solo per mostrare. Vale prima il nome che scegli tu, poi quello dell'emittente, poi quello del file. Con «Mostra tutti i titoli» puoi sceglierne uno da qui.");
        E("nav:/names", "Nomi dei titoli", "L'elenco dei nomi che hai dato ai titoli, per rivederli e cambiarli.", keywords: "nomi titoli isin rinominare alias nome codice");
        E("names.dialog", "Nome del titolo", "Qui scegli il nome di un titolo. Resta lo stesso in tutto il programma finché non lo cambi. Lasciandolo vuoto torni al nome dell'emittente (per i derivati, se lo si è letto) o a quello del file.",
            "Il nome che scrivi si usa nelle tabelle, nella ricerca rapida e negli export. Se dai lo stesso nome a due titoli, nelle statistiche per sottostante finiscono nello stesso gruppo.", "nome titolo rinominare isin codice");
        E("table.compact", "Righe compatte", "Riduce l'altezza delle righe di tutte le tabelle per vederne di più. La scelta resta anche alla prossima apertura.", keywords: "righe compatte tabella densità");
        E("names.all", "Mostra tutti i titoli", "Con la spunta vedi tutti i titoli del conto e non solo quelli che hai rinominato, così puoi dare un nome anche agli altri.", keywords: "mostra tutti titoli nomi");
        E("names.export", "Esporta i nomi", "Salva i tuoi nomi in un CSV con due colonne (isin e nome): utile come copia o per correggerli in Excel.", keywords: "esporta nomi csv");
        E("names.import", "Importa i nomi", "Legge un CSV con due colonne (codice e nome, anche con il separatore «;») e dà quei nomi ai codici. Se un codice ha già un nome, viene sostituito.", keywords: "importa nomi csv");

        // ---- Versione 2.5: statistiche di trading
        E("page:/stats", "Statistiche di trading", "Quanto spesso guadagni, quanto vale in media un guadagno rispetto a una perdita, quanto durano le posizioni e in quali giorni rendi di più.",
            "Ogni «operazione» è un ciclo completo di una posizione: dal primo acquisto a quando la quantità torna a zero. Contano anche i cicli già chiusi di titoli che hai ricomprato, e i turbo azzerati senza una vendita registrata (chiusi in perdita). Gli importi sono netti di commissioni e imposte trattenute.");
        E("nav:group", "Gruppi del menu", "Le pagine sono raggruppate in cinque gruppi: Oggi, Operatività, Fisco, Mercati e Dati. Clicca il nome di un gruppo per aprirlo: gli altri si chiudono. Quello della pagina in cui sei si apre da solo.", keywords: "menu gruppi aprire chiudere navigazione");
        E("nav:/stats", "Statistiche", "Numeri di sintesi sulle tue operazioni chiuse: percentuale in guadagno, guadagno e perdita medi, durata, giorni migliori.", keywords: "statistiche trading percentuale vincenti guadagno medio fattore di profitto durata calendario giorni mesi sottostante");
        E("stats.year", "Anno di chiusura", "Calcola le statistiche solo sulle operazioni chiuse in quell'anno.", keywords: "anno chiusura filtro");
        E("stats.trades", "Operazioni chiuse", "Quante operazioni complete (da acquisto a vendita totale) hai chiuso, e quante sono state aperte e chiuse nello stesso giorno.", keywords: "operazioni chiuse numero stesso giorno");
        E("stats.winrate", "Percentuale in guadagno", "Su cento operazioni, quante hai chiuso in guadagno. Da sola non basta: conta anche quanto guadagni e quanto perdi.", keywords: "percentuale guadagno vincenti win rate");
        E("stats.average", "Guadagno medio", "Quanto guadagni in media in un'operazione in guadagno, e quanto perdi in media in una in perdita. Il rapporto dice quante perdite vale un guadagno: con una percentuale bassa serve un rapporto alto.", keywords: "guadagno medio perdita media rapporto payoff");
        E("stats.expectancy", "Risultato medio per operazione", "Il risultato totale diviso per il numero di operazioni: quanto ti rende, in media, ogni operazione.", keywords: "risultato medio operazione aspettativa expectancy");
        E("stats.profitfactor", "Fattore di profitto", "La somma di tutti i guadagni divisa per la somma di tutte le perdite. Sopra 1 in tutto guadagni, sotto 1 in tutto perdi. Vuoto se non ci sono perdite.", keywords: "fattore profitto profit factor guadagni perdite");
        E("stats.fees", "Commissioni pagate", "Le commissioni pagate sulle operazioni e, se il risultato lordo è positivo, quanta parte ne hanno mangiata.", keywords: "commissioni costi risultato lordo");
        E("stats.duration", "Durata media", "Quanti giorni dura in media una posizione, dal primo acquisto alla vendita finale. Sotto la durata, le serie più lunghe di operazioni di fila in guadagno e in perdita.", keywords: "durata giorni serie vincite perdite consecutive");
        E("stats.extremes", "Migliore e peggiore", "L'operazione chiusa con il risultato più alto e quella con il più basso.", keywords: "migliore peggiore operazione");
        E("stats.equity", "Risultato cumulato", "Il risultato realizzato delle operazioni chiuse, sommato giorno dopo giorno: se sale guadagni, se scende perdi.", keywords: "risultato cumulato curva grafico");
        E("stats.months", "Risultato per mese", "Guadagni (verde) e perdite (rosso) delle operazioni chiuse in ogni mese.", keywords: "risultato mese grafico guadagni perdite");
        E("stats.calendar", "Giorno per giorno", "Un quadratino per ogni giorno in cui hai chiuso operazioni, colorato in base al risultato di quel giorno. Passa il mouse su un quadratino per leggere la data e l'importo.", keywords: "calendario giorni mappa colore risultato giornaliero");
        E("stats.weekday", "Per giorno della settimana", "Quante operazioni chiudi e come vanno nei vari giorni della settimana.", keywords: "giorno settimana lunedì venerdì risultato");
        E("stats.underlying", "Per sottostante", "I risultati raggruppati per titolo di riferimento: tutti i turbo sulla stessa azione finiscono in una sola riga.", keywords: "sottostante titolo turbo raggruppati");
        E("stats/Sottostante", "Sottostante", "Il titolo di riferimento: i prodotti con lo stesso sottostante sono uniti in una riga.", keywords: "sottostante");
        E("stats/Percentuale in guadagno", "Percentuale in guadagno", "Su cento operazioni su quel sottostante, quante sono andate in guadagno.", keywords: "percentuale guadagno");

        // ---- Versione 2.4: storico delle operazioni
        E("top:add", "Aggiungi movimenti", "Aggiunge ai tuoi movimenti quelli di un altro CSV (per esempio solo l'ultimo mese scaricato dal broker). L'app riconosce le operazioni che ha già e aggiunge solo quelle nuove.",
            "Le operazioni si riconoscono dall'identificativo di transazione del file. Dopo l'aggiunta l'app usa lo «storico delle operazioni», l'unione di tutti i file aggiunti, e ti dice cosa è stato aggiunto e se tra i due file potrebbe mancare qualcosa.", "aggiungi movimenti unisci importa csv storico nuovi");
        E("files.archive", "Storico delle operazioni", "L'unione di tutti i CSV che hai aggiunto, tenuta nella cartella dati. Serve per non dover scaricare ogni volta tutti i movimenti: basta aggiungere i più recenti.",
            "I tuoi file CSV non vengono mai modificati. Prima di ogni aggiornamento dello storico se ne tiene una copia (operazioni.prev.bak). Puoi esportarlo in CSV quando vuoi.", "storico operazioni unione archivio csv");
        E("merge.dialog", "Esito dell'aggiunta", "Quante operazioni nuove sono state aggiunte, quante c'erano già, se ci sono conflitti e se tra i due insiemi di movimenti potrebbe mancare un periodo.",
            "Un conflitto è una riga con lo stesso identificativo di una già presente ma con dati diversi (per esempio una correzione del broker): in quel caso resta la riga già presente.", "esito aggiunta conflitti sovrapposizione");
        E("file:operazioni.csv", "operazioni.csv", "Lo storico delle operazioni: l'unione di tutti i CSV che hai aggiunto, nello stesso formato dell'esportazione.", keywords: "storico operazioni csv");
        E("file:operazioni.prev.bak", "operazioni.prev.bak", "La copia dello storico prima dell'ultimo aggiornamento, per tornare indietro se qualcosa va storto.", keywords: "copia storico precedente");
        E("nav:language", "Lingua", "Cambia la lingua dell'app (italiano, inglese, francese, tedesco, spagnolo). Vale subito per testi, numeri, date e importi, e si ricorda alla prossima apertura.", keywords: "lingua traduzione italiano inglese francese tedesco spagnolo");
        E("nav:/currencies", "Valute", "Cambi contro l'euro, convertitore ed esposizione del conto.", keywords: "valute cambi dollaro sterlina franco yen forex convertitore");
        E("fx.refresh", "Aggiorna", "Scarica adesso i cambi. Di base la pagina si aggiorna da sola ogni minuto mentre è aperta.", keywords: "aggiorna cambi");
        E("fx.auto", "Aggiorna ogni minuto", "Con la spunta, mentre la pagina è aperta i cambi si riscaricano ogni 60 secondi.", keywords: "automatico aggiornamento minuto");
        E("fx.status", "Ultimo aggiornamento", "Quando sono stati scaricati i cambi e da quale fonte. Con Yahoo Finance sono di pochi secondi fa; con la BCE sono quelli di riferimento dell'ultimo giorno lavorativo.", keywords: "aggiornamento fonte");
        E("fx.cards", "Cambi principali", "I quattro cambi più seguiti contro l'euro. Clicca per aprire il grafico.", keywords: "cambi principali dollaro sterlina franco yen");
        E("fx.table", "Tabella dei cambi", "Quante unità di ogni valuta valgono 1 euro, con le variazioni e un mini grafico. Clicca una riga per vedere lo storico.", keywords: "tabella cambi valute");
        E("fx.col.rate", "1 € vale", "Quante unità di quella valuta si ottengono con 1 euro. Esempio: 1,12 per il dollaro significa che 1 € = 1,12 $.", keywords: "cambio euro vale");
        E("fx.col.inverse", "1 unità vale", "Il contrario: quanti euro vale una unità della valuta (1 ÷ cambio).", keywords: "inverso cambio");
        E("fx.col.change1d", "Oggi", "Di quanto è cambiato il cambio rispetto alla chiusura precedente.",
            "Il cambio è «quante unità per 1 €»: se il numero sale (+) l'euro si rafforza e quella valuta si indebolisce; se scende (−) succede il contrario. Per questo non è colorato di verde o di rosso.", "oggi variazione giornaliera");
        E("fx.col.change30d", "30 giorni", "La variazione del cambio rispetto a 30 giorni fa (cambio BCE di quel giorno). Il segno si legge come nella colonna «Oggi».", keywords: "30 giorni variazione mensile");
        E("fx.col.spark", "Ultimi 30 giorni", "Il mini grafico dei cambi degli ultimi 30 giorni, con il valore di adesso in fondo.", keywords: "mini grafico andamento");
        E("fx.col.source", "Fonte", "Il pallino verde indica un valore di mercato di Yahoo Finance (di pochi secondi fa); grigio è il cambio di riferimento BCE. Passa il mouse per vedere data e ora.", keywords: "fonte yahoo bce");
        E("fx.chart", "Andamento", "L'andamento storico del cambio scelto: 1 euro in quella valuta. Cambia periodo con i pulsanti in alto a destra.", keywords: "andamento storico grafico cambio");
        E("fx.convert", "Convertitore", "Converte un importo tra due valute usando i cambi qui sopra (si passa sempre dall'euro). Il pulsante con le frecce inverte le due valute.", keywords: "convertitore converti importo");
        E("fx.exposure", "Il tuo conto per valuta", "Quanta parte di titoli, obbligazioni e crypto è quotata in ciascuna valuta, e quanto cambierebbe il valore se quella valuta si muovesse della percentuale che scegli.",
            "È la valuta di quotazione: un ETF quotato in euro che investe in dollari risulta in euro. La liquidità è sempre in euro e non è inclusa.", "esposizione valutaria rischio cambio");
        E("fx.shock", "Ipotesi di movimento", "Scegli di quanto cambia il valore delle valute diverse dall'euro rispetto all'euro (per esempio −5 = perdono il 5%): vedi l'effetto in euro su ogni valuta. Non è una previsione.", keywords: "ipotesi simulazione rischio cambio");

        // ---- Versione 2.3: Titoli di Stato
        E("page:/bonds", "Titoli di Stato",
            "Scegli lo Stato (Italia, Germania, Francia, Spagna, Stati Uniti e gli altri Stati europei) e vedi il rendimento a 10 anni, la curva dei rendimenti, lo spread sui Bund e i tuoi titoli di quello Stato.",
            "Per l'Italia c'è anche l'elenco dei singoli BTP con il calcolatore. Per gli altri Stati l'app mostra i rendimenti di riferimento ufficiali, non i prezzi dei singoli titoli.");
        E("nav:/bonds", "Titoli di Stato", "Rendimenti, curve e spread degli Stati europei e degli USA; per l'Italia l'elenco dei BTP con il calcolatore.", keywords: "titoli di stato btp bund oat bonos treasury rendimenti spread obbligazioni");
        E("bonds.countries", "Scegli lo Stato", "I paesi più seguiti sono qui sopra; gli altri Stati europei sono nell'elenco «Altri Stati». L'Italia apre l'elenco dei BTP.", keywords: "stato paese scegli italia germania francia spagna");
        E("bonds.refresh", "Aggiorna", "Riscarica adesso i dati di questo Stato. Si riscaricano comunque da soli dopo mezz'ora.", keywords: "aggiorna dati stato");
        E("bonds.btplist", "Elenco BTP e calcolatore", "Apre l'elenco dei singoli BTP da Borsa Italiana, con il calcolatore per simulare un acquisto.", keywords: "btp elenco calcolatore");
        E("bonds.yields", "Rendimenti e spread dell'Italia", "Apre la pagina con il rendimento a 10 anni dell'Italia e lo spread sui Bund tedeschi.", keywords: "rendimenti spread italia");
        E("bonds.card.yield", "Rendimento a 10 anni", "Il rendimento annuo di un titolo di Stato con scadenza a 10 anni: il riferimento più usato per confrontare gli Stati.",
            "Per Germania, USA e area euro il dato è giornaliero; per gli altri Stati è la media del mese, pubblicata dalla BCE con circa un mese e mezzo di ritardo.", "rendimento 10 anni benchmark");
        E("bonds.card.change", "Rispetto a un anno fa", "Di quanto è salito (+) o sceso (−) il rendimento in un anno, in punti percentuali (pp): da 3,0% a 3,5% sono +0,5 pp.", keywords: "variazione anno punti percentuali");
        E("bonds.card.slope", "Curva 10 − 2 anni", "Il rendimento a 10 anni meno quello a 2 anni. Positivo è la situazione normale; negativo (curva invertita) è spesso letto come un segnale di rallentamento.", keywords: "curva inversione pendenza 10 2 anni");
        E("bonds.card.spread", "Spread sui Bund", "La differenza di rendimento a 10 anni con i Bund tedeschi, in punti base (100 pb = 1%): è il «premio» che il mercato chiede in più per prestare a quello Stato.", keywords: "spread bund differenziale punti base");
        E("bonds.card.currency", "Valuta", "Questo Stato ha una valuta propria: il suo rendimento non è confrontabile direttamente con quello dei Bund in euro.", keywords: "valuta propria");
        E("bonds.chart.yield", "Rendimento a 10 anni", "L'andamento del rendimento a 10 anni nel tempo. Per gli Stati dell'area euro la linea tratteggiata è la Germania. Cambia periodo con i pulsanti.", keywords: "rendimento andamento grafico 10 anni");
        E("bonds.chart.spread", "Spread sui Bund tedeschi", "La differenza di rendimento con la Germania nel tempo, in punti base. Sale quando il mercato percepisce più rischio.", keywords: "spread grafico bund");
        E("bonds.chart.curve", "Curva dei rendimenti", "Il rendimento per ogni scadenza, da 1 a 30 anni. Le linee tratteggiate sono un mese fa e un anno fa: si vede come si è spostata.", keywords: "curva rendimenti scadenze");
        E("bonds.auctions", "Ultime aste del Tesoro USA", "Le emissioni più recenti di Note, Bond, TIPS: scadenza, cedola e rendimento. Sono i valori dell'asta, non quotazioni di mercato di oggi.", keywords: "aste treasury note bond usa");
        E("bonds.btc", "Copertura", "Quante volte le richieste hanno superato l'offerta all'asta (bid-to-cover): più è alto, più la domanda era forte.", keywords: "copertura bid to cover domanda");
        E("bonds.held", "I tuoi titoli di questo Stato", "Le obbligazioni che possiedi emesse da questo Stato (lo dicono le prime due lettere dell'ISIN), con quantità, valore e plus/minus latente. Clicca per aprirne il dettaglio.", keywords: "tuoi titoli posseduti obbligazioni");
        E("bonds.sources", "Da dove arrivano i dati", "Le fonti ufficiali e i loro ritardi. Per i prezzi dei singoli titoli fuori dall'Italia non esiste una fonte gratuita e ufficiale.", keywords: "fonti dati bce bundesbank tesoro");

        E("coupons.unknown", "Obbligazione senza dati", "L'obbligazione non è nell'elenco dei BTP a tasso fisso (cedola e scadenza): senza quei dati non si può calcolare il calendario.", keywords: "obbligazione senza dati");
        E("checks.rerun", "Ricontrolla", "Rifà tutte le verifiche con i dati attuali.", keywords: "ricontrolla verifiche");
        E("check:cash", "Liquidità", "Controlla che il saldo in cassa non scenda mai sotto zero: se succede, nel file mancano versamenti o altre operazioni.", keywords: "liquidita negativa cassa");
        E("check:negshares", "Quantità negative", "Controlla che nessun asset sia stato venduto più di quanto acquistato.", keywords: "quantita negative vendite");
        E("check:noquote", "Quotazioni mancanti", "Controlla che azioni, fondi e crypto aperti abbiano un prezzo scaricato (altrimenti sono valutati all'ultima operazione).", keywords: "quotazioni mancanti");
        E("check:stale", "Prezzi vecchi", "Controlla che le quotazioni non abbiano più di 3 giorni.", keywords: "prezzi vecchi aggiornati");
        E("check:pricejump", "Prezzi sospetti", "Confronta ogni quotazione con l'ultimo prezzo a cui hai comprato o venduto: uno scarto oltre il 50% può essere un cambio di valuta o un simbolo sbagliato.", keywords: "prezzi sospetti scarto valuta");
        E("check:currency", "Valute", "Controlla che tutte le quotazioni siano in euro: quelle in altre valute vengono ignorate finché non sono convertite.", keywords: "valute euro conversione");
        E("check:mapping", "Titoli non trovati", "Segnala titoli di cui Yahoo Finance non conosce il simbolo (magari ritirati dal mercato).", keywords: "titoli non trovati yahoo simbolo");
        E("check:duplicates", "Duplicati", "Controlla che nessuna operazione sia contata due volte (stesso identificativo).", keywords: "duplicati operazioni");
        E("check:freshness", "Quanto è recente il file", "Dice quando è l'ultima operazione del file: se è vecchia, esporta un file nuovo.", keywords: "file recente ultima operazione");

        // ---- Storia e nome
        E("page:/history", "Storia", "Le versioni dell'app, dalla più recente: com'è nata l'idea e come è cresciuta aggiornamento dopo aggiornamento.",
            "Le versioni 0.x e 1.x sono state numerate a posteriori, ricostruendo l'ordine del lavoro. Questa pagina viene aggiornata a ogni nuova versione.");
        E("nav:/history", "Storia", "Com'è nata l'app e tutte le versioni, dalla prima a oggi.", keywords: "storia versioni novita aggiornamenti cronologia");
        E("history.note", "Numeri di versione", "La prima cifra cambia solo per un rifacimento importante, la seconda per ogni gruppo di novità.", keywords: "numeri versione numerazione");
        E("name.pelago", "Perché si chiama Pelago",
            "«Pelago» in italiano è il mare aperto: rende l'idea di navigare tra i propri soldi. L'icona è una barca a vela sulle onde.",
            "Prima si chiamava DPOper («Oper» come operazioni). Tra Pelago, Quota, Bussola e Fiorino ha vinto Pelago. Il file si chiama pelago.exe, tutto minuscolo.",
            "nome pelago perche chiama dpoper icona barca vela mare");
        foreach (var release in ChangeLog.Releases)
        {
            E("release:" + release.Version, $"Versione {release.Version}: {release.Title}",
                $"{release.Date.ToString("d MMMM yyyy", Fmt.It)} — {release.Summary}", null,
                $"versione storia novita {release.Title} {release.Summary}");
        }

        // ---- Credits
        E("credits.sources", "Fonti dati", "Da dove arrivano i prezzi: CoinGecko, Yahoo Finance, Frankfurter (BCE) e Borsa Italiana.", keywords: "fonti dati coingecko yahoo frankfurter borsa italiana");
        E("credits.components", "Componenti", "Le librerie open source usate dall'app, con la loro licenza.", keywords: "componenti librerie licenze echarts tabler");
        E("credits.disclaimer", "Avvertenze", "I prezzi sono indicativi e l'app non dà consulenza finanziaria né calcoli fiscali.", keywords: "avvertenze consulenza fiscale");

        return d;
    }
}


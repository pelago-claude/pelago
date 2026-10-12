using DPOper.Models;

namespace DPOper.Services;

/// <summary>
/// Il valore del conto a fine giornata, suddiviso per tipologia. <see cref="Contributions"/> è il
/// saldo netto cumulato dei soli bonifici da/verso conti esterni (non include gli interessi, che
/// restano nel Totale come rendimento e non come versamento): confrontato col Totale mostra quanto
/// il conto è sopra o sotto rispetto a quanto vi è stato effettivamente versato dall'esterno.
/// </summary>
public sealed record DailySnapshot(
    DateOnly Date,
    decimal Cash,
    decimal Securities,
    decimal Bonds,
    decimal Crypto,
    decimal Contributions)
{
    public decimal Total => Cash + Securities + Bonds + Crypto;
    public decimal Gap => Total - Contributions;
}

/// <summary>
/// Una posizione (titolo/fondo/crypto/obbligazione/derivato) movimentata almeno una volta,
/// con la quantità attualmente posseduta e il suo valore stimato all'ultimo prezzo noto
/// (<see cref="LastPriceTimestamp"/> indica quando quel prezzo è stato rilevato).
/// </summary>
public sealed record Holding(
    string Key,
    string Name,
    string Symbol,
    string AssetClass,
    decimal Shares,
    decimal? LastPrice,
    DateTime? LastPriceTimestamp)
{
    public decimal Value => Shares * (LastPrice ?? 0m);
}

/// <summary>Un asset posseduto a una certa data, con quantità e prezzo noti a quel momento.</summary>
public sealed record AssetAsOf(string Key, string Name, string Symbol, string AssetClass, decimal Shares, decimal? Price)
{
    public decimal Value => Shares * (Price ?? 0m);
}

/// <summary>
/// La composizione del conto a una data precisa: liquidità e asset ancora posseduti a quel momento,
/// più i versamenti netti cumulati fino a quella data (vedi <see cref="DailySnapshot.Contributions"/>).
/// </summary>
public sealed record PortfolioAsOf(DateOnly Date, decimal Cash, List<AssetAsOf> Assets, decimal Contributions)
{
    public decimal AssetsTotal => Assets.Sum(a => a.Value);
    public decimal Total => Cash + AssetsTotal;
    public decimal Gap => Total - Contributions;
}

/// <summary>
/// Ricostruisce, giorno per giorno, la liquidità disponibile e il valore delle posizioni possedute.
///
/// Il file di export non contiene prezzi di mercato giornalieri: solo il prezzo al momento di ogni
/// operazione. Il valore di titoli/obbligazioni/crypto viene quindi stimato moltiplicando le quantità
/// possedute per l'ultimo prezzo noto di quell'asset (l'ultima operazione vista su quel simbolo).
/// Passando <c>externalQuotes</c> (es. dallo storico quotazioni aggiornato manualmente) questi punti
/// vengono uniti alla timeline dei prezzi da transazione, così anche un giorno senza movimenti ma con
/// una quotazione aggiornata produce/aggiorna una riga.
/// </summary>
public static class PortfolioCalculator
{
    public static List<DailySnapshot> Compute(
        IEnumerable<Operation> operations,
        IReadOnlyDictionary<string, List<(DateTime Timestamp, decimal Price)>>? externalQuotes = null)
    {
        var ordered = operations.OrderBy(o => o.DateTime).ThenBy(o => o.Date).ToList();
        if (ordered.Count == 0)
        {
            return new List<DailySnapshot>();
        }

        var priceTimeline = BuildPriceTimeline(ordered, externalQuotes);

        // Stato (cassa + quantità possedute + versamenti netti) alla FINE di ogni giorno con almeno
        // un'operazione.
        var checkpoints = new List<(DateOnly Date, decimal Cash, decimal Contributions,
            Dictionary<string, decimal> Shares, Dictionary<string, string> AssetClasses)>();

        var cashBalance = 0m;
        var contributions = 0m;
        var shareholdings = new Dictionary<string, decimal>();
        var assetClassByKey = new Dictionary<string, string>();
        DateOnly? currentDate = null;

        void Checkpoint(DateOnly date) => checkpoints.Add((date, cashBalance, contributions,
            new Dictionary<string, decimal>(shareholdings), new Dictionary<string, string>(assetClassByKey)));

        foreach (var op in ordered)
        {
            if (currentDate is not null && op.Date != currentDate)
            {
                Checkpoint(currentDate.Value);
            }
            currentDate = op.Date;

            cashBalance += op.NetCashImpact;

            if (op.Category == "CASH" && op.Type.Contains("TRANSFER", StringComparison.OrdinalIgnoreCase))
            {
                contributions += op.NetCashImpact;
            }

            var key = op.PositionKey;
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(op.AssetClass))
            {
                if (op.Shares is { } shares && shares != 0m)
                {
                    shareholdings[key] = shareholdings.GetValueOrDefault(key) + shares;
                }
                assetClassByKey[key] = op.AssetClass;
            }
        }
        Checkpoint(currentDate!.Value);

        // Giorni per cui produrre una riga: ogni giorno con un'operazione, più ogni giorno con una
        // quotazione esterna (non prima della prima operazione).
        var firstDate = checkpoints[0].Date;
        var snapshotDates = new SortedSet<DateOnly>(checkpoints.Select(c => c.Date));
        foreach (var points in priceTimeline.Values)
        {
            foreach (var (timestamp, _) in points)
            {
                var d = DateOnly.FromDateTime(timestamp);
                if (d >= firstDate)
                {
                    snapshotDates.Add(d);
                }
            }
        }

        var snapshots = new List<DailySnapshot>();
        var checkpointIndex = 0;
        var pricePointer = priceTimeline.Keys.ToDictionary(k => k, _ => 0);

        foreach (var date in snapshotDates)
        {
            while (checkpointIndex + 1 < checkpoints.Count && checkpoints[checkpointIndex + 1].Date <= date)
            {
                checkpointIndex++;
            }
            var (_, cash, snapshotContributions, shares, assetClasses) = checkpoints[checkpointIndex];

            var securities = 0m;
            var bonds = 0m;
            var crypto = 0m;
            var endOfDay = date.ToDateTime(TimeOnly.MaxValue);

            foreach (var (key, quantity) in shares)
            {
                if (quantity == 0m || !priceTimeline.TryGetValue(key, out var timeline) || timeline.Count == 0)
                {
                    continue;
                }

                var idx = pricePointer[key];
                while (idx + 1 < timeline.Count && timeline[idx + 1].Timestamp <= endOfDay)
                {
                    idx++;
                }
                pricePointer[key] = idx;

                if (timeline[idx].Timestamp > endOfDay)
                {
                    continue;
                }

                var value = quantity * timeline[idx].Price;
                switch (assetClasses.GetValueOrDefault(key, ""))
                {
                    case "BOND":
                        bonds += value;
                        break;
                    case "CRYPTO":
                        crypto += value;
                        break;
                    default:
                        securities += value;
                        break;
                }
            }

            snapshots.Add(new DailySnapshot(date, cash, securities, bonds, crypto, snapshotContributions));
        }

        return snapshots;
    }

    /// <summary>
    /// Elenca tutti gli asset movimentati almeno una volta, con la quantità attualmente posseduta
    /// (può essere 0 se la posizione è stata interamente chiusa) e l'ultimo prezzo noto.
    ///
    /// Se per un asset è disponibile una quotazione da <c>externalQuotes</c> (recuperata via API),
    /// questa ha SEMPRE la priorità sul prezzo dell'ultima transazione, anche se quest'ultima ha una
    /// data più recente: la transazione è il prezzo a cui è stato eseguito uno scambio passato, la
    /// quotazione è la valutazione corrente esplicitamente richiesta. Solo se non è mai stata
    /// recuperata una quotazione si ricade sull'ultimo prezzo di transazione.
    /// </summary>
    public static List<Holding> ComputeHoldings(
        IEnumerable<Operation> operations,
        IReadOnlyDictionary<string, List<(DateTime Timestamp, decimal Price)>>? externalQuotes = null)
    {
        var ordered = operations.OrderBy(o => o.DateTime).ToList();
        var transactionPrices = BuildPriceTimeline(ordered, externalQuotes: null);

        var shareholdings = new Dictionary<string, decimal>();
        var info = new Dictionary<string, (string Name, string Symbol, string AssetClass)>();

        foreach (var op in ordered)
        {
            var key = op.PositionKey;
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(op.AssetClass))
            {
                continue;
            }

            if (op.Shares is { } shares && shares != 0m)
            {
                shareholdings[key] = shareholdings.GetValueOrDefault(key) + shares;
            }

            info[key] = (op.Name, op.Symbol, op.AssetClass);
        }

        return info.Select(kv =>
            {
                decimal? lastPrice = null;
                DateTime? lastPriceTimestamp = null;

                if (externalQuotes is not null && externalQuotes.TryGetValue(kv.Key, out var quotePoints) && quotePoints.Count > 0)
                {
                    var latestQuote = quotePoints.OrderByDescending(q => q.Timestamp).First();
                    lastPrice = latestQuote.Price;
                    lastPriceTimestamp = latestQuote.Timestamp;
                }
                else if (transactionPrices.TryGetValue(kv.Key, out var timeline) && timeline.Count > 0)
                {
                    lastPrice = timeline[^1].Price;
                    lastPriceTimestamp = timeline[^1].Timestamp;
                }

                return new Holding(
                    kv.Key,
                    kv.Value.Name,
                    kv.Value.Symbol,
                    kv.Value.AssetClass,
                    shareholdings.GetValueOrDefault(kv.Key),
                    lastPrice,
                    lastPriceTimestamp);
            })
            .OrderBy(h => h.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Composizione del conto a una data precisa: quantità possedute a quel momento (gli asset non
    /// ancora comprati o già chiusi entro quella data non compaiono) e prezzo più recente noto
    /// <em>fino a quella data</em> (non oltre — è un punto nel tempo, non "l'ultimo prezzo assoluto").
    /// </summary>
    public static PortfolioAsOf ComputeAsOf(
        IEnumerable<Operation> operations,
        DateOnly asOfDate,
        IReadOnlyDictionary<string, List<(DateTime Timestamp, decimal Price)>>? externalQuotes = null)
    {
        var ordered = operations.OrderBy(o => o.DateTime).ThenBy(o => o.Date).ToList();
        var priceTimeline = BuildPriceTimeline(ordered, externalQuotes);
        var endOfDay = asOfDate.ToDateTime(TimeOnly.MaxValue);

        var cashBalance = 0m;
        var contributions = 0m;
        var shareholdings = new Dictionary<string, decimal>();
        var info = new Dictionary<string, (string Name, string Symbol, string AssetClass)>();

        foreach (var op in ordered)
        {
            if (op.DateTime > endOfDay)
            {
                break;
            }

            cashBalance += op.NetCashImpact;

            if (op.Category == "CASH" && op.Type.Contains("TRANSFER", StringComparison.OrdinalIgnoreCase))
            {
                contributions += op.NetCashImpact;
            }

            var key = op.PositionKey;
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(op.AssetClass))
            {
                if (op.Shares is { } shares && shares != 0m)
                {
                    shareholdings[key] = shareholdings.GetValueOrDefault(key) + shares;
                }
                info[key] = (op.Name, op.Symbol, op.AssetClass);
            }
        }

        var assets = info
            .Where(kv => shareholdings.GetValueOrDefault(kv.Key) != 0m)
            .Select(kv =>
            {
                var price = priceTimeline.TryGetValue(kv.Key, out var timeline)
                    ? timeline.Where(p => p.Timestamp <= endOfDay).Select(p => (decimal?)p.Price).LastOrDefault()
                    : null;

                return new AssetAsOf(kv.Key, kv.Value.Name, kv.Value.Symbol, kv.Value.AssetClass,
                    shareholdings[kv.Key], price);
            })
            .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new PortfolioAsOf(asOfDate, cashBalance, assets, contributions);
    }

    private static Dictionary<string, List<(DateTime Timestamp, decimal Price)>> BuildPriceTimeline(
        List<Operation> ordered,
        IReadOnlyDictionary<string, List<(DateTime Timestamp, decimal Price)>>? externalQuotes)
    {
        var timeline = new Dictionary<string, List<(DateTime, decimal)>>();

        foreach (var op in ordered)
        {
            if (op.Price is not { } price || price == 0m)
            {
                continue;
            }
            var key = op.PositionKey;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }
            if (!timeline.TryGetValue(key, out var list))
            {
                list = new List<(DateTime, decimal)>();
                timeline[key] = list;
            }
            list.Add((op.DateTime, price));
        }

        if (externalQuotes is not null)
        {
            foreach (var (key, points) in externalQuotes)
            {
                if (!timeline.TryGetValue(key, out var list))
                {
                    list = new List<(DateTime, decimal)>();
                    timeline[key] = list;
                }
                list.AddRange(points);
            }
        }

        foreach (var list in timeline.Values)
        {
            list.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        }

        return timeline;
    }
}

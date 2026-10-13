using Pelago.Models;

namespace Pelago.Services;

/// <summary>Rendimento di un periodo: guadagno in euro, rendimento ponderato nel tempo (TWR) e per i flussi di cassa (XIRR).</summary>
public sealed record ReturnPeriod(
    string Label,
    DateOnly From,
    DateOnly To,
    decimal StartValue,
    decimal NetDeposits,
    decimal EndValue,
    double? Twr,
    double? Xirr)
{
    /// <summary>Guadagno (o perdita) del periodo: valore finale − valore iniziale − versamenti netti.</summary>
    public decimal Gain => EndValue - StartValue - NetDeposits;

    /// <summary>Il periodo dura meno di un anno: l'XIRR annualizzato è poco significativo.</summary>
    public bool Short => To.DayNumber - From.DayNumber < 365;
}

public sealed record CategoryReturn(string Label, double? Twr, decimal StartValue, decimal EndValue, decimal NetFlows)
{
    public decimal Gain => EndValue - StartValue - NetFlows;
}

/// <summary>
/// Rendimenti "veri" del conto. L'idea: confrontare soltanto il Totale con i versamenti netti non dice quanto
/// hai reso, perché non tiene conto di QUANDO hai versato (mille euro versati ieri non hanno avuto il tempo di rendere
/// quanto mille euro versati un anno fa).
///
/// <list type="bullet">
/// <item><b>TWR</b> (rendimento ponderato nel tempo): concatena i rendimenti giornalieri (metodo di Dietz, vedi <see cref="DailyReturn"/>), togliendo l'effetto dei versamenti
/// e dei prelievi. Dice come è andata la gestione, a prescindere da quando hai messo i soldi.</item>
/// <item><b>XIRR</b> (rendimento ponderato per i flussi di cassa): il tasso annuo che rende nullo il valore dei tuoi versamenti
/// e del valore finale, tenendo conto delle date. Dice come sono andati <i>i tuoi</i> soldi.</item>
/// </list>
/// I flussi sono i bonifici verso/da conti esterni, ipotizzati a metà giornata per il TWR e a fine giornata per l'XIRR. Il valore del conto è quello degli
/// snapshot giornalieri (ultimo prezzo noto di ogni asset).
/// </summary>
public static class ReturnCalculator
{
    /// <summary>Rendimento ponderato nel tempo tra due date (esclusa <paramref name="from"/>, inclusa <paramref name="to"/>). Null se non c'è capitale su cui calcolarlo.</summary>
    public static double? Twr(IReadOnlyList<DailySnapshot> snapshots, DateOnly from, DateOnly to)
    {
        var list = snapshots.OrderBy(s => s.Date).ToList();
        if (list.Count < 2)
        {
            return null;
        }

        var start = StartIndex(list, from);
        var growth = 1.0;
        var any = false;

        for (var i = start + 1; i < list.Count && list[i].Date <= to; i++)
        {
            var previous = list[i - 1];
            var flow = list[i].Contributions - previous.Contributions;
            if (DailyReturn(previous.Total, list[i].Total, flow) is not { } r)
            {
                continue; // prima del primo versamento non c'è capitale su cui misurare un rendimento
            }

            growth *= 1.0 + r;
            any = true;
        }

        return any ? growth - 1.0 : null;
    }

    /// <summary>
    /// Rendimento di una giornata con il metodo di Dietz: (valore finale − valore iniziale − flusso) / (valore iniziale + metà del flusso).
    /// I bonifici arrivano durante la giornata (di solito la mattina e vengono investiti subito), quindi contano per metà nel capitale
    /// su cui si misura il rendimento: così un versamento enorme in un conto piccolo non fa uscire percentuali impossibili.
    /// Null se non c'è capitale; mai sotto −100%.
    /// </summary>
    internal static double? DailyReturn(decimal startValue, decimal endValue, decimal flow)
    {
        if (startValue <= 0m)
        {
            return null;
        }

        var denominator = startValue + 0.5m * flow;
        if (denominator <= 0m)
        {
            return null;
        }

        return Math.Max(-1.0, (double)((endValue - startValue - flow) / denominator));
    }

    /// <summary>Serie del rendimento cumulato (TWR) giorno per giorno, per il grafico.</summary>
    public static List<(DateOnly Date, double Cumulative)> CumulativeTwr(IReadOnlyList<DailySnapshot> snapshots)
    {
        var list = snapshots.OrderBy(s => s.Date).ToList();
        var result = new List<(DateOnly, double)>();
        var growth = 1.0;

        for (var i = 1; i < list.Count; i++)
        {
            var previous = list[i - 1];
            if (DailyReturn(previous.Total, list[i].Total, list[i].Contributions - previous.Contributions) is { } r)
            {
                growth *= 1.0 + r;
                result.Add((list[i].Date, growth - 1.0));
            }
        }
        return result;
    }

    /// <summary>
    /// Tasso annuo che azzera il valore attuale dei flussi (importi con segno: i versamenti sono negativi, il valore finale
    /// positivo), con giorni/365 come in Excel. Null se non esiste una soluzione (flussi tutti dello stesso segno).
    /// </summary>
    public static double? Xirr(IReadOnlyList<(DateOnly Date, decimal Amount)> flows)
    {
        if (flows.Count < 2 || !flows.Any(f => f.Amount > 0m) || !flows.Any(f => f.Amount < 0m))
        {
            return null;
        }

        var origin = flows.Min(f => f.Date);
        double Npv(double rate) => flows.Sum(f => (double)f.Amount / Math.Pow(1.0 + rate, (f.Date.DayNumber - origin.DayNumber) / 365.0));

        double low = -0.9999, high = 10.0;
        var npvLow = Npv(low);
        var npvHigh = Npv(high);
        if (double.IsNaN(npvLow) || double.IsNaN(npvHigh) || Math.Sign(npvLow) == Math.Sign(npvHigh))
        {
            return null;
        }

        for (var i = 0; i < 200; i++)
        {
            var mid = (low + high) / 2.0;
            var npvMid = Npv(mid);
            if (Math.Abs(npvMid) < 1e-7)
            {
                return mid;
            }
            if (Math.Sign(npvMid) == Math.Sign(npvLow))
            {
                low = mid;
                npvLow = npvMid;
            }
            else
            {
                high = mid;
            }
        }
        return (low + high) / 2.0;
    }

    /// <summary>Un periodo completo: valori, versamenti, guadagno, TWR e XIRR.</summary>
    public static ReturnPeriod Period(string label, IReadOnlyList<DailySnapshot> snapshots, IEnumerable<Operation> operations, DateOnly from, DateOnly to)
    {
        var list = snapshots.OrderBy(s => s.Date).ToList();
        var startIndex = list.Count == 0 ? -1 : StartIndex(list, from);
        var startSnapshot = startIndex >= 0 ? list[startIndex] : null;
        var startValue = startSnapshot is { } s0 && s0.Date <= from ? s0.Total : 0m;
        var startContrib = startSnapshot is { } s1 && s1.Date <= from ? s1.Contributions : 0m;
        var end = list.LastOrDefault(s => s.Date <= to);
        var endValue = end?.Total ?? 0m;
        var netDeposits = (end?.Contributions ?? 0m) - startContrib;

        var flows = new List<(DateOnly Date, decimal Amount)>();
        if (startValue > 0m)
        {
            flows.Add((from, -startValue));
        }
        foreach (var op in operations.Where(o => IsTransfer(o) && o.Date > from && o.Date <= to))
        {
            flows.Add((op.Date, -op.NetCashImpact));
        }
        if (end is not null)
        {
            flows.Add((to < end.Date ? to : end.Date, endValue));
        }

        return new ReturnPeriod(label, from, to, startValue, netDeposits, endValue, Twr(list, from, to), Xirr(flows));
    }

    /// <summary>Rendimento per ogni anno solare presente nei dati, più il totale dall'inizio.</summary>
    public static List<ReturnPeriod> ByYear(IReadOnlyList<DailySnapshot> snapshots, IEnumerable<Operation> operations)
    {
        var ops = operations.ToList();
        var list = snapshots.OrderBy(s => s.Date).ToList();
        var result = new List<ReturnPeriod>();
        if (list.Count == 0)
        {
            return result;
        }

        var first = list[0].Date;
        var last = list[^1].Date;
        for (var year = first.Year; year <= last.Year; year++)
        {
            var from = new DateOnly(year - 1, 12, 31);
            var to = year == last.Year ? last : new DateOnly(year, 12, 31);
            result.Add(Period(year == last.Year ? Loc.T("{0} (fino a oggi)", year) : year.ToString(), list, ops, from < first ? first.AddDays(-1) : from, to));
        }
        return result;
    }

    /// <summary>Rendimento ponderato nel tempo di ogni categoria (titoli, obbligazioni, crypto), dai flussi di acquisto e vendita.</summary>
    public static List<CategoryReturn> ByCategory(IReadOnlyList<DailySnapshot> snapshots, IEnumerable<Operation> operations)
    {
        var list = snapshots.OrderBy(s => s.Date).ToList();
        var ops = operations.ToList();
        var result = new List<CategoryReturn>();

        foreach (var (label, belongs, pick) in new (string, Func<string, bool>, Func<DailySnapshot, decimal>)[]
        {
            (Loc.T("Titoli (azioni, fondi, derivati)"), c => c is "STOCK" or "FUND" or "DERIVATIVE", s => s.Securities),
            (Loc.T("Obbligazioni"), c => c == "BOND", s => s.Bonds),
            (Loc.T("Crypto"), c => c == "CRYPTO", s => s.Crypto),
        })
        {
            // Flusso verso la categoria in un giorno: acquisti (cassa che esce) meno vendite. Le quantità ricevute senza
            // cassa (regali, esercizi) non sono flussi: sono rendimento.
            var flowByDate = ops
                .Where(o => belongs(o.AssetClass) && o.Category == "TRADING")
                .GroupBy(o => o.Date)
                .ToDictionary(g => g.Key, g => -g.Sum(o => o.NetCashImpact));

            var growth = 1.0;
            var any = false;
            var netFlows = 0m;
            for (var i = 1; i < list.Count; i++)
            {
                var previous = list[i - 1];
                var flow = flowByDate.Where(kv => kv.Key > previous.Date && kv.Key <= list[i].Date).Sum(kv => kv.Value);
                netFlows += flow;
                if (DailyReturn(pick(previous), pick(list[i]), flow) is not { } r)
                {
                    continue;
                }
                growth *= 1.0 + r;
                any = true;
            }

            var first = list.Count > 0 ? pick(list[0]) : 0m;
            var end = list.Count > 0 ? pick(list[^1]) : 0m;
            result.Add(new CategoryReturn(label, any ? growth - 1.0 : null, first, end, netFlows));
        }
        return result;
    }

    private static bool IsTransfer(Operation o) =>
        o.Category == "CASH" && o.Type.Contains("TRANSFER", StringComparison.OrdinalIgnoreCase);

    /// <summary>Indice dell'ultimo snapshot con data non successiva a <paramref name="from"/> (o il primo, se non ce n'è).</summary>
    private static int StartIndex(List<DailySnapshot> list, DateOnly from)
    {
        var index = 0;
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Date <= from)
            {
                index = i;
            }
            else
            {
                break;
            }
        }
        return index;
    }
}

using System.Text.RegularExpressions;
using DPOper.Models;

namespace DPOper.Services;

/// <summary>
/// Un «ciclo» di una posizione: dal primo acquisto (la quantità sale da zero) fino a quando la quantità torna a zero. Se compri, vendi tutto e
/// ricompri più avanti sono due cicli distinti. Importi in euro, ricavati dai movimenti di cassa: <see cref="Invested"/> è quanto è uscito
/// per acquistare (commissioni comprese), <see cref="Proceeds"/> quanto è entrato dalle cessioni (al netto di commissioni e imposte trattenute).
/// </summary>
public sealed record PositionCycle(
    int Number,
    DateTime Opened,
    DateTime Closed,
    int Trades,
    decimal Invested,
    decimal Proceeds,
    decimal Fees,
    decimal TaxesPaid,
    bool KnockedOut)
{
    /// <summary>Risultato netto del ciclo: quanto è entrato meno quanto è uscito.</summary>
    public decimal Result => Proceeds - Invested;

    /// <summary>Risultato prima di commissioni e imposte trattenute.</summary>
    public decimal GrossResult => Result + Fees + TaxesPaid;

    public double? ReturnPct => Invested > 0m ? (double)(Result / Invested) : null;

    public int DaysHeld => Math.Max(0, (int)(Closed.Date - Opened.Date).TotalDays);
}

/// <summary>Un titolo con almeno un ciclo concluso: tutti i suoi cicli (anche quello ancora aperto non c'è: vedi <see cref="IsClosed"/>).</summary>
public sealed record ClosedPosition(string Key, string Name, string AssetClass, IReadOnlyList<PositionCycle> Cycles, bool IsClosed)
{
    public decimal Result => Cycles.Sum(c => c.Result);
    public decimal Invested => Cycles.Sum(c => c.Invested);
    public decimal Proceeds => Cycles.Sum(c => c.Proceeds);
    public decimal Fees => Cycles.Sum(c => c.Fees);
    public decimal TaxesPaid => Cycles.Sum(c => c.TaxesPaid);
    public int Trades => Cycles.Sum(c => c.Trades);
    public DateTime Opened => Cycles.Min(c => c.Opened);
    public DateTime Closed => Cycles.Max(c => c.Closed);
    public bool KnockedOut => Cycles.Any(c => c.KnockedOut);
    public double? ReturnPct => Invested > 0m ? (double)(Result / Invested) : null;
}

/// <summary>
/// Posizioni chiuse: per ogni titolo, i cicli acquisto → vendita completi. Si scorre la storia di ogni titolo in ordine di tempo, tenendo la quantità:
/// quando sale da zero comincia un ciclo, quando torna a zero finisce. I turbo e i warrant che scadono o vanno in knock-out senza una vendita
/// registrata (la quantità resta aperta ma il prezzo è ormai zero) si considerano chiusi a perdita: lo dice <see cref="PositionCycle.KnockedOut"/>.
/// </summary>
public static class ClosedPositionCalculator
{
    private const decimal Epsilon = 0.000000001m;

    /// <summary>Un prezzo a o sotto questa soglia, su un derivato ancora «aperto», significa knock-out o scadenza.</summary>
    public const decimal KnockOutPrice = 0.01m;

    private static readonly Regex Isin = new(@"\b[A-Z]{2}[A-Z0-9]{9}\d\b", RegexOptions.Compiled);

    /// <param name="operations">Tutti i movimenti.</param>
    /// <param name="lastPrice">Ultimo prezzo noto di un titolo (per riconoscere i knock-out); null se non si vuole.</param>
    public static List<ClosedPosition> Compute(IEnumerable<Operation> operations, Func<string, decimal?>? lastPrice = null)
    {
        var all = OperationCleanup.WithoutReversals(operations);

        // incassi di esercizio dei warrant (righe «TILG» senza quantità): si attribuiscono al titolo indicato dall'ISIN nella descrizione
        var settlements = all.Where(o => o.Type == "TILG" && o.NetCashImpact != 0m)
            .Select(o => (Operation: o, Key: Isin.Match(o.Description) is { Success: true } m ? m.Value : null))
            .Where(x => x.Key is not null)
            .ToLookup(x => x.Key!, x => x.Operation);

        var result = new List<ClosedPosition>();
        foreach (var group in all.Where(o => !string.IsNullOrWhiteSpace(o.PositionKey) && !string.IsNullOrWhiteSpace(o.AssetClass)).GroupBy(o => o.PositionKey))
        {
            var cycles = new List<PositionCycle>();
            var shares = 0m;
            var opened = default(DateTime);
            var last = default(DateTime);
            decimal invested = 0m, proceeds = 0m, fees = 0m, taxes = 0m;
            var trades = 0;

            PositionCycle Close(DateTime when, bool knockedOut) =>
                new(cycles.Count + 1, opened, when, trades, invested, proceeds, fees, taxes, knockedOut);

            foreach (var op in group.OrderBy(o => o.DateTime))
            {
                var delta = op.Shares ?? 0m;
                if (delta == 0m)
                {
                    continue;
                }

                if (shares <= Epsilon)
                {
                    // nuovo ciclo
                    shares = 0m;
                    opened = op.DateTime;
                    invested = proceeds = fees = taxes = 0m;
                    trades = 0;
                }

                trades++;
                last = op.DateTime;
                fees += -(op.Fee ?? 0m);
                taxes += -(op.Tax ?? 0m);

                if (delta > 0m)
                {
                    shares += delta;
                    invested += op.NetCashImpact < 0m ? -op.NetCashImpact : 0m;
                    continue;
                }

                shares += delta;
                proceeds += op.NetCashImpact;
                if (shares <= Epsilon)
                {
                    shares = 0m;
                    cycles.Add(Close(op.DateTime, knockedOut: false));
                }
            }

            var first = group.First();
            var isClosed = shares <= Epsilon;
            if (!isClosed && first.AssetClass == "DERIVATIVE" && lastPrice?.Invoke(group.Key) is { } price && price <= KnockOutPrice)
            {
                // ancora «aperto» ma senza più valore: knock-out o scadenza, chiuso a perdita
                cycles.Add(Close(last, knockedOut: true));
                isClosed = true;
            }

            // l'incasso in contanti di un esercizio arriva dopo la chiusura: va al ciclo chiuso più di recente prima della sua data (uno solo, mai due)
            if (cycles.Count > 0)
            {
                foreach (var settlement in settlements[group.Key])
                {
                    var index = cycles.FindLastIndex(c => c.Closed <= settlement.DateTime);
                    index = index < 0 ? 0 : index;
                    cycles[index] = cycles[index] with { Proceeds = cycles[index].Proceeds + settlement.NetCashImpact };
                }
            }

            if (cycles.Count > 0)
            {
                var name =AssetNameResolver.ResolveDisplayName(first.Name, first.AssetClass, group.Select(o => o.Description));
                result.Add(new ClosedPosition(group.Key, name, first.AssetClass, cycles, isClosed));
            }
        }

        return result;
    }

    /// <summary>
    /// Il «sottostante» di un nome di prodotto: toglie la parte tra parentesi («Best Turbo auf ACME INC. (Long 206,84 $)» diventa
    /// «Best Turbo auf ACME INC.»), così centinaia di turbo sullo stesso titolo si raggruppano.
    /// </summary>
    public static string Underlying(string name) => Regex.Replace(name, @"\s*\((?:Long|Short)[^)]*\)\s*$", "").Trim();
}

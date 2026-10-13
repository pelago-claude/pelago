using Pelago.Models;

namespace Pelago.Services;

/// <summary>
/// Costi e imposte di un periodo, espressi come importi PAGATI (positivi = soldi usciti, negativi = rimborsi).
/// </summary>
public sealed record CostRow(
    string Period,
    decimal Fees,
    decimal TradeTax,
    decimal InterestTax,
    decimal StampAndRecalculation,
    decimal TaxOptimization,
    decimal OtherTax)
{
    /// <summary>Imposte nette (pagate meno rimborsate).</summary>
    public decimal NetTax => TradeTax + InterestTax + StampAndRecalculation + TaxOptimization + OtherTax;

    /// <summary>Commissioni più imposte nette.</summary>
    public decimal Total => Fees + NetTax;
}

/// <summary>
/// Somma commissioni e imposte dell'export. Le commissioni sono la colonna «fee»; le imposte sono la colonna «tax»,
/// divisa per tipo di operazione: compravendite (BUY/SELL), interessi, imposta di bollo e ricalcoli (TAX),
/// ottimizzazione fiscale (TAX_OPTIMIZATION: di solito rimborsi) e altro. Nell'export i costi hanno segno negativo:
/// qui vengono girati in positivo (pagato) per leggere la tabella senza pensarci.
/// </summary>
public static class CostCalculator
{
    public static List<CostRow> ByYear(IEnumerable<Operation> operations) =>
        Build(operations, o => o.Date.Year.ToString("0000")).OrderBy(r => r.Period).ToList();

    public static List<CostRow> ByMonth(IEnumerable<Operation> operations) =>
        Build(operations, o => $"{o.Date.Year:0000}-{o.Date.Month:00}").OrderBy(r => r.Period).ToList();

    public static CostRow Total(IEnumerable<Operation> operations) =>
        Build(operations, _ => "Totale").FirstOrDefault() ?? new CostRow("Totale", 0, 0, 0, 0, 0, 0);

    private static IEnumerable<CostRow> Build(IEnumerable<Operation> operations, Func<Operation, string> periodOf) =>
        operations.GroupBy(periodOf).Select(g => new CostRow(
            g.Key,
            Fees: -g.Sum(o => o.Fee ?? 0m),
            TradeTax: -g.Where(o => o.Type is "BUY" or "SELL" or "SELL_CANCELLED").Sum(o => o.Tax ?? 0m),
            InterestTax: -g.Where(o => o.Type == "INTEREST_PAYMENT").Sum(o => o.Tax ?? 0m),
            StampAndRecalculation: -g.Where(o => o.Type == "TAX").Sum(o => o.Tax ?? 0m),
            TaxOptimization: -g.Where(o => o.Type == "TAX_OPTIMIZATION").Sum(o => o.Tax ?? 0m),
            OtherTax: -g.Where(o => o.Type is not ("BUY" or "SELL" or "SELL_CANCELLED" or "INTEREST_PAYMENT" or "TAX" or "TAX_OPTIMIZATION"))
                .Sum(o => o.Tax ?? 0m)));
}

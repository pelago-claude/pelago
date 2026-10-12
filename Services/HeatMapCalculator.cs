using DPOper.Models;

namespace DPOper.Services;

/// <summary>Un titolo posseduto a una certa data, per la mappa: quanto vale, quanto è costato e quanto pesa sul conto.</summary>
public sealed record HeatItem(string Key, string Name, string Symbol, string AssetClass, decimal Shares, decimal? Price, decimal Value, decimal? Cost, double Weight)
{
    /// <summary>Guadagno o perdita sul costo (quello che ancora non hai incassato); null se il costo non si conosce.</summary>
    public decimal? Pnl => Cost is > 0m ? Value - Cost : null;

    /// <summary>Guadagno o perdita in percentuale del costo; null se il costo non si conosce.</summary>
    public double? PnlPct => Cost is > 0m ? (double)((Value - Cost.Value) / Cost.Value) : null;
}

/// <param name="Hidden">Quanti titoli posseduti non compaiono perché valgono zero (per esempio turbo azzerati) o non hanno un prezzo noto.</param>
public sealed record HeatMap(DateOnly Date, decimal Total, IReadOnlyList<HeatItem> Items, int Hidden)
{
    public decimal ItemsValue => Items.Sum(i => i.Value);

    /// <summary>Guadagno o perdita complessivi sui titoli di cui si conosce il costo.</summary>
    public decimal Pnl => Items.Sum(i => i.Pnl ?? 0m);

    /// <summary>Il costo dei titoli di cui si conosce il costo.</summary>
    public decimal KnownCost => Items.Where(i => i.Pnl is not null).Sum(i => i.Cost ?? 0m);
}

/// <summary>
/// La composizione dei titoli a una data (come «Composizione a una data»: quantità possedute quel giorno e ultimo prezzo noto fino a quel giorno) con, per
/// ciascuno, il costo medio ponderato di quel momento: da lì il guadagno o la perdita ancora aperti, che danno il colore della mappa.
/// Per le date passate i prezzi sono quelli che l'app conosce (quotazioni registrate e prezzi dei tuoi scambi): dove mancano, il valore è approssimato.
/// </summary>
public static class HeatMapCalculator
{
    public static HeatMap Compute(IReadOnlyList<Operation> operations, DateOnly date,
        IReadOnlyDictionary<string, List<(DateTime Timestamp, decimal Price)>>? quotes = null)
    {
        var asOf = PortfolioCalculator.ComputeAsOf(operations, date, quotes);
        var end = date.ToDateTime(TimeOnly.MaxValue);
        var open = RealizedGainsCalculator.ComputeOpenPositions(operations.Where(o => o.DateTime <= end));
        var total = asOf.Total;

        var held = asOf.Assets.Where(a => a.Shares > 0m).ToList();
        var shown = held.Where(a => a.Value > 0m).ToList();
        var items = shown
            .Select(a => new HeatItem(a.Key, a.Name, a.Symbol, a.AssetClass, a.Shares, a.Price, a.Value,
                open.TryGetValue(a.Key, out var position) ? position.Cost : null,
                total > 0m ? (double)(a.Value / total) : 0.0))
            .OrderByDescending(i => i.Value)
            .ToList();
        return new HeatMap(date, total, items, held.Count - shown.Count);
    }
}

/// <summary>
/// Il colore di un titolo sulla mappa: grigio se il guadagno è zero o non si sa, verde se è in guadagno e rosso se è in perdita, più intenso quanto più
/// è lontano da zero. Oltre il 50% (in più o in meno) il colore è al massimo: i turbo si muovono di centinaia di punti, e con una scala più larga
/// tutto il resto sarebbe grigio.
/// </summary>
public static class HeatColor
{
    /// <summary>Il guadagno (o la perdita) oltre il quale il colore non diventa più intenso.</summary>
    public const double Saturation = 0.5;

    private static readonly (int R, int G, int B) Neutral = (0x6f, 0x7b, 0x8a);
    private static readonly (int R, int G, int B) Gain = (0x1e, 0x8a, 0x6e);
    private static readonly (int R, int G, int B) Loss = (0xb8, 0x3a, 0x24);

    public static string NeutralHex => Hex(Neutral);

    public static string GainHex => Hex(Gain);

    public static string LossHex => Hex(Loss);

    /// <summary>Il colore (#rrggbb) per un guadagno in percentuale del costo (0,1 = +10%); null se il costo non si conosce.</summary>
    public static string For(double? pct)
    {
        if (pct is not { } value || double.IsNaN(value))
        {
            return Hex(Neutral);
        }

        var t = Math.Min(Math.Abs(value) / Saturation, 1.0);
        var target = value >= 0 ? Gain : Loss;
        return Hex((Mix(Neutral.R, target.R, t), Mix(Neutral.G, target.G, t), Mix(Neutral.B, target.B, t)));
    }

    private static int Mix(int from, int to, double t) => (int)Math.Round(from + (to - from) * t);

    private static string Hex((int R, int G, int B) c) => $"#{c.R:x2}{c.G:x2}{c.B:x2}";
}

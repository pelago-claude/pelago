using DPOper.Models;
using DPOper.Services;

namespace DPOper.Forms;

/// <summary>
/// Elenca le plusvalenze/minusvalenze realizzate su ogni cessione (a costo medio ponderato),
/// con una riga di totale finale.
/// </summary>
public sealed class RealizedGainsForm : GridFormBase
{
    private static readonly Dictionary<string, string> AssetClassLabels = new()
    {
        ["STOCK"] = "Azione",
        ["FUND"] = "Fondo",
        ["BOND"] = "Obbligazione",
        ["CRYPTO"] = "Crypto",
        ["DERIVATIVE"] = "Derivato",
    };

    private sealed record GainRow(DateTime? Date, string Name, string Symbol, string AssetClassLabel,
        decimal? SharesDisposed, decimal Proceeds, decimal CostBasis, decimal Gain);

    public RealizedGainsForm(List<Operation> operations)
        : base("RealizedGainsForm", "Plusvalenze/Minusvalenze realizzate", 1000, 550)
    {
        AddColumn("Date", "Data", "dd/MM/yyyy", DataGridViewContentAlignment.MiddleLeft, 80);
        AddColumn("Name", "Nome", null, DataGridViewContentAlignment.MiddleLeft, 220);
        AddColumn("Symbol", "Simbolo", null, DataGridViewContentAlignment.MiddleLeft, 100);
        AddColumn("AssetClassLabel", "Categoria", null, DataGridViewContentAlignment.MiddleLeft, 90);
        AddColumn("SharesDisposed", "Quantità ceduta", "N4", DataGridViewContentAlignment.MiddleRight, 100);
        AddColumn("Proceeds", "Ricavo", "C2", DataGridViewContentAlignment.MiddleRight, 90);
        AddColumn("CostBasis", "Costo (medio)", "C2", DataGridViewContentAlignment.MiddleRight, 90);
        AddColumn("Gain", "Plus/Minus", "C2", DataGridViewContentAlignment.MiddleRight, 90);

        var events = RealizedGainsCalculator.Compute(operations);

        var rows = events.Select(ev => new GainRow(
            ev.Date,
            AssetNameResolver.ResolveDisplayName(ev.Name, ev.AssetClass, new[] { ev.Description }),
            ev.Symbol,
            AssetClassLabels.GetValueOrDefault(ev.AssetClass, ev.AssetClass),
            ev.SharesDisposed,
            ev.Proceeds,
            ev.CostBasis,
            ev.Gain)).ToList();

        var totalGain = rows.Sum(r => r.Gain);
        var totalProceeds = rows.Sum(r => r.Proceeds);
        var totalCost = rows.Sum(r => r.CostBasis);
        var totalRow = new GainRow(null, "SALDO", "", "", null, totalProceeds, totalCost, totalGain);

        Grid.DataSource = rows;
        Grid.CellFormatting += Grid_CellFormatting;
        SetTotals(totalRow);

        var gains = rows.Count(r => r.Gain >= 0);
        var losses = rows.Count - gains;
        SetStatus($"{rows.Count} cessioni ({gains} in utile, {losses} in perdita) — Plus/minusvalenza totale: {totalGain:C2}. " +
                   "Calcolo a costo medio ponderato, non è una dichiarazione fiscale.");
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (Grid.Columns[e.ColumnIndex].Name != "Gain" || e.Value is not decimal gain)
        {
            return;
        }

        e.CellStyle!.ForeColor = gain >= 0 ? Color.DarkGreen : Color.DarkRed;
    }
}

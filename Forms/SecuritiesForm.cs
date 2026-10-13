using Pelago.Models;
using Pelago.Services;

namespace Pelago.Forms;

/// <summary>
/// Elenca gli asset movimentati (di default tutte le classi), con la quantità attualmente
/// posseduta e il valore stimato all'ultimo prezzo noto (da transazione o, se più recente, dallo
/// storico quotazioni aggiornato manualmente). Per le posizioni chiuse (quantità 0), al posto del
/// valore di mercato (sempre 0) mostra il saldo netto in cassa generato da quella posizione
/// nell'intera storia. Doppio click su una riga apre lo storico completo dei movimenti.
/// </summary>
public sealed class SecuritiesForm : GridFormBase
{
    private static readonly Dictionary<string, string> AssetClassLabels = new()
    {
        ["STOCK"] = "Azione",
        ["FUND"] = "Fondo",
        ["BOND"] = "Obbligazione",
        ["CRYPTO"] = "Crypto",
        ["DERIVATIVE"] = "Derivato",
    };

    private readonly List<Operation> _operations;

    private sealed record HoldingRow(string Key, string Name, string Symbol, string AssetClassLabel,
        decimal? Shares, decimal? LastPrice, DateTime? LastPriceTimestamp, decimal Value, decimal? ClosedBalance);

    public SecuritiesForm(List<Operation> operations, string title = "Titoli movimentati",
        Func<string, bool>? assetClassFilter = null,
        IReadOnlyDictionary<string, List<(DateTime Timestamp, decimal Price)>>? externalQuotes = null)
        : base("SecuritiesForm", title, 1000, 550)
    {
        _operations = operations;

        AddColumn("Name", "Nome", null, DataGridViewContentAlignment.MiddleLeft, 260);
        AddColumn("Symbol", "Simbolo", null, DataGridViewContentAlignment.MiddleLeft, 110);
        AddColumn("AssetClassLabel", "Categoria", null, DataGridViewContentAlignment.MiddleLeft, 90);
        AddColumn("Shares", "Quantità", "N4", DataGridViewContentAlignment.MiddleRight, 100);
        AddColumn("LastPrice", "Ultimo prezzo", "C4", DataGridViewContentAlignment.MiddleRight, 100);
        AddColumn("LastPriceTimestamp", "Quotazione del", "dd/MM/yyyy HH:mm", DataGridViewContentAlignment.MiddleLeft, 100);
        AddColumn("Value", "Valore stimato", "C2", DataGridViewContentAlignment.MiddleRight, 100);
        AddColumn("ClosedBalance", "Saldo (posizione chiusa)", "C2", DataGridViewContentAlignment.MiddleRight, 120);

        var holdings = PortfolioCalculator.ComputeHoldings(operations, externalQuotes);
        if (assetClassFilter is not null)
        {
            holdings = holdings.Where(h => assetClassFilter(h.AssetClass)).ToList();
        }

        var rows = holdings.Select(h =>
        {
            var history = operations.Where(o => o.PositionKey == h.Key).ToList();
            var displayName = AssetNameResolver.ResolveDisplayName(h.Name, h.AssetClass, history.Select(o => o.Description));
            var isClosed = h.Shares == 0m;
            var closedBalance = isClosed ? history.Sum(o => o.NetCashImpact) : (decimal?)null;

            return new HoldingRow(
                h.Key,
                displayName,
                h.Symbol,
                AssetClassLabels.GetValueOrDefault(h.AssetClass, h.AssetClass),
                h.Shares,
                h.LastPrice ?? 0m,
                h.LastPriceTimestamp?.ToLocalTime(),
                h.Value,
                closedBalance);
        }).ToList();

        var openPositions = rows.Count(h => h.Shares != 0m);
        var closedRows = rows.Where(h => h.ClosedBalance is not null).ToList();
        var totalValue = rows.Sum(h => h.Value);
        var totalClosedBalance = closedRows.Sum(h => h.ClosedBalance!.Value);
        var totalRow = new HoldingRow("", "SALDO", "", "", null, null, null, totalValue, totalClosedBalance);

        Grid.DataSource = rows;
        Grid.CellDoubleClick += Grid_CellDoubleClick;
        Grid.CellFormatting += Grid_CellFormatting;
        SetTotals(totalRow);

        SetStatus($"{rows.Count} asset movimentati ({openPositions} con posizione aperta, {closedRows.Count} chiuse) — " +
                   $"Valore stimato posizioni aperte: {totalValue:C2}   Saldo posizioni chiuse: {totalClosedBalance:C2}. " +
                   "Doppio click su una riga per vedere i movimenti.");
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (Grid.Columns[e.ColumnIndex].Name != "ClosedBalance" || e.Value is not decimal balance)
        {
            return;
        }

        e.CellStyle!.ForeColor = balance >= 0 ? Color.DarkGreen : Color.DarkRed;
    }

    private void Grid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || Grid.Rows[e.RowIndex].DataBoundItem is not HoldingRow row)
        {
            return;
        }

        var history = _operations.Where(o => o.PositionKey == row.Key).ToList();

        var detail = new SecurityDetailForm(row.Name, history) { MdiParent = MdiParent };
        detail.Show();
    }
}

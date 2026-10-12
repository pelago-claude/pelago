using DPOper.Models;
using DPOper.Services;

namespace DPOper.Forms;

/// <summary>
/// Elenca tutti gli asset presenti nel catalogo quotazioni, con data e valore dell'ultimo
/// aggiornamento registrato. Doppio click su una riga apre lo storico completo delle quotazioni
/// di quell'asset.
/// </summary>
public sealed class AssetQuotesForm : GridFormBase
{
    private static readonly Dictionary<string, string> AssetClassLabels = new()
    {
        ["STOCK"] = "Azione",
        ["FUND"] = "Fondo",
        ["BOND"] = "Obbligazione",
        ["CRYPTO"] = "Crypto",
        ["DERIVATIVE"] = "Derivato",
        ["FX"] = "Cambio",
    };

    private readonly Dictionary<string, AssetCatalogEntry> _catalogByKey;

    private sealed record AssetQuoteRow(string Key, string Name, string Symbol, string AssetClassLabel,
        int QuoteCount, DateTime? LastUpdate, decimal? LastValue, string LastCurrency);

    public AssetQuotesForm() : base("AssetQuotesForm", "Elenco quotazioni")
    {
        AddColumn("Name", "Nome", null, DataGridViewContentAlignment.MiddleLeft, 220);
        AddColumn("Symbol", "Simbolo", null, DataGridViewContentAlignment.MiddleLeft, 100);
        AddColumn("AssetClassLabel", "Categoria", null, DataGridViewContentAlignment.MiddleLeft, 90);
        AddColumn("QuoteCount", "N. aggiornamenti", "N0", DataGridViewContentAlignment.MiddleRight, 100);
        AddColumn("LastUpdate", "Ultimo aggiornamento", "dd/MM/yyyy HH:mm", DataGridViewContentAlignment.MiddleLeft, 130);
        AddColumn("LastValue", "Ultimo valore", "N4", DataGridViewContentAlignment.MiddleRight, 100);
        AddColumn("LastCurrency", "Valuta", null, DataGridViewContentAlignment.MiddleCenter, 60);

        HideTotalsFooter();

        var catalog = AssetCatalogStore.Load();
        _catalogByKey = catalog.ToDictionary(e => e.Key);

        var rows = catalog.Select(entry =>
        {
            var quotes = QuoteHistoryStore.Load(entry);
            var last = quotes.OrderByDescending(q => q.Timestamp).FirstOrDefault();
            return new AssetQuoteRow(
                entry.Key,
                entry.Name,
                entry.Symbol,
                AssetClassLabels.GetValueOrDefault(entry.AssetClass, entry.AssetClass),
                quotes.Count,
                last?.Timestamp.ToLocalTime(),
                last?.Price,
                last?.Currency ?? "");
        })
        .OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
        .ToList();

        Grid.DataSource = rows;
        Grid.CellDoubleClick += Grid_CellDoubleClick;

        var neverUpdated = rows.Count(r => r.LastUpdate is null);
        SetStatus($"{rows.Count} asset nel catalogo ({neverUpdated} mai aggiornati) — " +
                   "doppio click su una riga per lo storico completo delle quotazioni.");
    }

    private void Grid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || Grid.Rows[e.RowIndex].DataBoundItem is not AssetQuoteRow row
            || !_catalogByKey.TryGetValue(row.Key, out var entry))
        {
            return;
        }

        var detail = new QuoteHistoryDetailForm(entry) { MdiParent = MdiParent };
        detail.Show();
    }
}

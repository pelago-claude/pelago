using Pelago.Models;
using Pelago.Services;

namespace Pelago.Forms;

/// <summary>
/// Storico completo delle quotazioni recuperate via API per un singolo asset.
/// </summary>
public sealed class QuoteHistoryDetailForm : GridFormBase
{
    private sealed record QuoteRow(DateTime Timestamp, decimal Price, string Currency, string Source);

    public QuoteHistoryDetailForm(AssetCatalogEntry entry)
        : base("QuoteHistoryDetailForm", $"Storico quotazioni - {entry.Name}", 600, 450)
    {
        AddColumn("Timestamp", "Data", "dd/MM/yyyy HH:mm:ss", DataGridViewContentAlignment.MiddleLeft, 130);
        AddColumn("Price", "Prezzo", "N4", DataGridViewContentAlignment.MiddleRight, 100);
        AddColumn("Currency", "Valuta", null, DataGridViewContentAlignment.MiddleCenter, 60);
        AddColumn("Source", "Fonte", null, DataGridViewContentAlignment.MiddleLeft, 100);

        HideTotalsFooter();

        var rows = QuoteHistoryStore.Load(entry)
            .OrderByDescending(q => q.Timestamp)
            .Select(q => new QuoteRow(q.Timestamp.ToLocalTime(), q.Price, q.Currency, q.Source))
            .ToList();

        Grid.DataSource = rows;

        SetStatus($"{rows.Count} quotazioni registrate per {entry.Name} ({entry.Symbol}).");
    }
}

using Pelago.Models;

namespace Pelago.Forms;

/// <summary>
/// Elenca i versamenti in arrivo e i prelievi verso conti esterni (bonifici), evidenziando
/// entrate e uscite, con una riga di totale finale.
/// </summary>
public sealed class TransfersForm : GridFormBase
{
    private sealed record TransferRow(DateTime? Date, string Direction, decimal Amount,
        string Counterparty, string Iban, string Reference, string Description);

    public TransfersForm(IEnumerable<Operation> operations)
        : base("TransfersForm", "Entrate/Uscite - Bonifici da/verso conti esterni")
    {
        AddColumn("Date", "Data", "dd/MM/yyyy HH:mm", DataGridViewContentAlignment.MiddleLeft, 90);
        AddColumn("Direction", "Direzione", null, DataGridViewContentAlignment.MiddleCenter, 70);
        AddColumn("Amount", "Importo", "C2", DataGridViewContentAlignment.MiddleRight, 90);
        AddColumn("Counterparty", "Contropartita", null, DataGridViewContentAlignment.MiddleLeft, 150);
        AddColumn("Iban", "IBAN", null, DataGridViewContentAlignment.MiddleLeft, 150);
        AddColumn("Reference", "Riferimento", null, DataGridViewContentAlignment.MiddleLeft, 120);
        AddColumn("Description", "Descrizione", null, DataGridViewContentAlignment.MiddleLeft, 200);

        var transfers = operations
            .Where(o => o.Category == "CASH" && o.Type.Contains("TRANSFER", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(o => o.DateTime)
            .Select(o => new TransferRow(
                o.DateTime,
                o.NetCashImpact >= 0 ? "Entrata" : "Uscita",
                o.NetCashImpact,
                o.CounterpartyName,
                o.CounterpartyIban,
                o.PaymentReference,
                o.Description))
            .ToList();

        var totalIn = transfers.Where(t => t.Amount >= 0).Sum(t => t.Amount);
        var totalOut = transfers.Where(t => t.Amount < 0).Sum(t => t.Amount);
        var totalRow = new TransferRow(null, "SALDO", totalIn + totalOut, "", "", "",
            $"{transfers.Count} movimenti — entrate {totalIn:C2}, uscite {totalOut:C2}");

        Grid.DataSource = transfers;
        Grid.CellFormatting += Grid_CellFormatting;
        SetTotals(totalRow);

        SetStatus($"{transfers.Count} movimenti — Totale entrate: {totalIn:C2}   Totale uscite: {totalOut:C2}   Netto: {(totalIn + totalOut):C2}");
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (Grid.Columns[e.ColumnIndex].Name != "Amount" || e.Value is not decimal amount)
        {
            return;
        }

        e.CellStyle!.ForeColor = amount >= 0 ? Color.DarkGreen : Color.DarkRed;
    }
}

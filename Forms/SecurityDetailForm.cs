using Pelago.Models;

namespace Pelago.Forms;

/// <summary>
/// Storico completo dei movimenti di un singolo asset (acquisti, vendite, ricevute gratuite,
/// esercizi di warrant, ecc.), con una riga di saldo finale.
/// </summary>
public sealed class SecurityDetailForm : GridFormBase
{
    private sealed record MovementRow(DateTime? Date, string Type, decimal? Shares, decimal? Price,
        decimal Amount, decimal Fee, decimal Tax, string Description);

    public SecurityDetailForm(string assetName, List<Operation> history)
        : base("SecurityDetailForm", $"Movimenti - {assetName}", 950, 500)
    {
        AddColumn("Date", "Data", "dd/MM/yyyy HH:mm", DataGridViewContentAlignment.MiddleLeft, 90);
        AddColumn("Type", "Tipo", null, DataGridViewContentAlignment.MiddleLeft, 90);
        AddColumn("Shares", "Quantità", "N4", DataGridViewContentAlignment.MiddleRight, 90);
        AddColumn("Price", "Prezzo", "C4", DataGridViewContentAlignment.MiddleRight, 90);
        AddColumn("Amount", "Importo", "C2", DataGridViewContentAlignment.MiddleRight, 90);
        AddColumn("Fee", "Fee", "C2", DataGridViewContentAlignment.MiddleRight, 70);
        AddColumn("Tax", "Tax", "C2", DataGridViewContentAlignment.MiddleRight, 70);
        AddColumn("Description", "Descrizione", null, DataGridViewContentAlignment.MiddleLeft, 260);

        var ordered = history
            .OrderByDescending(o => o.DateTime)
            .Select(o => new MovementRow(o.DateTime, o.Type, o.Shares, o.Price,
                o.Amount ?? 0m, o.Fee ?? 0m, o.Tax ?? 0m, o.Description))
            .ToList();

        var netShares = ordered.Sum(o => o.Shares ?? 0m);
        var totalAmount = ordered.Sum(o => o.Amount);
        var totalFee = ordered.Sum(o => o.Fee);
        var totalTax = ordered.Sum(o => o.Tax);
        var totalRow = new MovementRow(null, "SALDO", netShares, null, totalAmount, totalFee, totalTax,
            $"{ordered.Count} movimenti");

        Grid.DataSource = ordered;
        SetTotals(totalRow);

        SetStatus($"{ordered.Count} movimenti — Quantità netta posseduta: {netShares:N4}   " +
                   $"Netto in cassa: {(totalAmount + totalFee + totalTax):C2}");
    }
}

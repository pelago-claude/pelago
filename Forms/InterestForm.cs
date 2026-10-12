using DPOper.Models;

namespace DPOper.Forms;

/// <summary>
/// Elenca i pagamenti di interessi sulla liquidità, con importo lordo, tassa trattenuta e netto,
/// e una riga di totale finale.
/// </summary>
public sealed class InterestForm : GridFormBase
{
    private sealed record InterestRow(DateTime? Date, decimal Gross, decimal Tax, decimal Net, string Description);

    public InterestForm(IEnumerable<Operation> operations) : base("InterestForm", "Interessi", 700, 500)
    {
        AddColumn("Date", "Data", "dd/MM/yyyy", DataGridViewContentAlignment.MiddleLeft, 80);
        AddColumn("Gross", "Lordo", "C2", DataGridViewContentAlignment.MiddleRight, 80);
        AddColumn("Tax", "Tassa", "C2", DataGridViewContentAlignment.MiddleRight, 80);
        AddColumn("Net", "Netto", "C2", DataGridViewContentAlignment.MiddleRight, 80);
        AddColumn("Description", "Descrizione", null, DataGridViewContentAlignment.MiddleLeft, 260);

        var interests = operations
            .Where(o => o.Type == "INTEREST_PAYMENT")
            .OrderByDescending(o => o.DateTime)
            .Select(o => new InterestRow(o.DateTime, o.Amount ?? 0m, o.Tax ?? 0m, o.NetCashImpact, o.Description))
            .ToList();

        var totalGross = interests.Sum(i => i.Gross);
        var totalTax = interests.Sum(i => i.Tax);
        var totalNet = interests.Sum(i => i.Net);
        var totalRow = new InterestRow(null, totalGross, totalTax, totalNet, $"SALDO — {interests.Count} pagamenti");

        Grid.DataSource = interests;
        SetTotals(totalRow);

        SetStatus($"{interests.Count} pagamenti — Totale lordo: {totalGross:C2}   " +
                   $"Totale tasse: {totalTax:C2}   Totale netto: {totalNet:C2}");
    }
}

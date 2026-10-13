using Pelago.Services;

namespace Pelago.Forms;

/// <summary>
/// Vista principale: il saldo del conto giorno per giorno, suddiviso per disponibilità, titoli,
/// obbligazioni e crypto. È la prima finestra figlia che si apre nel contenitore MDI dopo aver
/// caricato un file.
/// </summary>
public sealed class DailyOverviewForm : GridFormBase
{
    public DailyOverviewForm(List<DailySnapshot> snapshots) : base("DailyOverviewForm", "Andamento giornaliero del conto")
    {
        AddColumn("Date", "Data", "dd/MM/yyyy", DataGridViewContentAlignment.MiddleLeft, 80);
        AddColumn("Cash", "Disponibilità", "C2", DataGridViewContentAlignment.MiddleRight, 100);
        AddColumn("Securities", "Titoli", "C2", DataGridViewContentAlignment.MiddleRight, 100);
        AddColumn("Bonds", "Obbligazioni", "C2", DataGridViewContentAlignment.MiddleRight, 100);
        AddColumn("Crypto", "Crypto", "C2", DataGridViewContentAlignment.MiddleRight, 100);
        var totalColumn = AddColumn("Total", "Totale", "C2", DataGridViewContentAlignment.MiddleRight, 100);
        totalColumn.DefaultCellStyle.Font = new Font(Font, FontStyle.Bold);

        HideTotalsFooter();

        Grid.DataSource = snapshots
            .OrderByDescending(s => s.Date)
            .Select(s => new
            {
                Date = s.Date.ToDateTime(TimeOnly.MinValue),
                s.Cash,
                s.Securities,
                s.Bonds,
                s.Crypto,
                s.Total,
            })
            .ToList();

        if (snapshots.Count > 0)
        {
            var latest = snapshots.OrderByDescending(s => s.Date).First();
            SetStatus($"{snapshots.Count} giorni — saldo attuale: {latest.Total.ToString("C2", ItCulture)} " +
                       $"(disponibilità {latest.Cash:C2}, titoli {latest.Securities:C2}, obbligazioni {latest.Bonds:C2}, crypto {latest.Crypto:C2})");
        }
    }
}

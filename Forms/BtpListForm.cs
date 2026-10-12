using DPOper.Models;
using DPOper.Services;

namespace DPOper.Forms;

/// <summary>
/// Elenco dei BTP italiani a tasso fisso con i dati di ciascuno (cedola, scadenza, prezzo, rateo, rendimenti),
/// scaricati online da Borsa Italiana: all'apertura si aggiorna da solo se i dati sono vecchi o assenti, e il
/// pulsante in alto li riscarica quando vuoi. Doppio click su un BTP: chiede il capitale da investire e apre
/// il calcolatore.
/// </summary>
public sealed class BtpListForm : GridFormBase
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(15);

    private sealed record Row(string Isin, string Description, decimal Coupon, DateTime Maturity, decimal Price,
        double YearsRemaining, decimal Accrued, decimal CurrentYield, double? GrossYield, double? NetYield);

    private readonly BtpData _data = BtpStore.Load();
    private readonly Label _infoLabel;
    private readonly Button _refreshButton;
    private readonly CancellationTokenSource _cancellation = new();
    private bool _refreshing;
    private int _skippedNoPrice;
    private List<string> _unrecognized = new();

    public BtpListForm() : base("BtpListForm", "Elenco BTP", 1150, 620)
    {
        AddColumn("Isin", "ISIN", null, DataGridViewContentAlignment.MiddleLeft, 110);
        AddColumn("Description", "Descrizione", null, DataGridViewContentAlignment.MiddleLeft, 170);
        AddColumn("Coupon", "Cedola annua", "0.0##%", DataGridViewContentAlignment.MiddleRight, 80);
        AddColumn("Maturity", "Scadenza", "dd/MM/yyyy", DataGridViewContentAlignment.MiddleLeft, 90);
        AddColumn("Price", "Prezzo", "0.00#", DataGridViewContentAlignment.MiddleRight, 80);
        AddColumn("YearsRemaining", "Anni residui", "N2", DataGridViewContentAlignment.MiddleRight, 80);
        AddColumn("Accrued", "Rateo per 100", "0.0000", DataGridViewContentAlignment.MiddleRight, 90);
        // Stessi nomi (e stesso calcolo) della schermata di dettaglio. Il rendimento cedolare sul capitale
        // investito è cedola annua / controvalore = 100 x cedola / prezzo: non dipende da capitale né commissioni.
        var couponYieldColumn = AddColumn("CurrentYield", "Rendimento cedolare lordo sul capitale investito", "P2", DataGridViewContentAlignment.MiddleRight, 130);
        var grossYieldColumn = AddColumn("GrossYield", "Rendimento effettivo a scadenza lordo (annuo)", "P2", DataGridViewContentAlignment.MiddleRight, 130);
        AddColumn("NetYield", "Rendimento effettivo a scadenza netto (annuo)", "P2", DataGridViewContentAlignment.MiddleRight, 130);

        // I due rendimenti lordi in evidenza, ciascuno con il suo colore (intestazione + celle in grassetto).
        // Con gli stili visuali di Windows l'intestazione ignora i colori: li disattivo per questa griglia.
        Grid.EnableHeadersVisualStyles = false;
        Grid.ColumnHeadersDefaultCellStyle.BackColor = SystemColors.Control;
        Grid.ColumnHeadersDefaultCellStyle.ForeColor = SystemColors.ControlText;
        Grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = SystemColors.Control;
        Grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = SystemColors.ControlText;
        Grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        Highlight(couponYieldColumn, Color.FromArgb(190, 215, 245), Color.FromArgb(228, 238, 251));
        Highlight(grossYieldColumn, Color.FromArgb(190, 228, 188), Color.FromArgb(230, 245, 228));
        HideTotalsFooter();

        _refreshButton = new Button { Text = "Aggiorna da Borsa Italiana", AutoSize = true, Margin = new Padding(0, 0, 12, 0) };
        _refreshButton.Click += async (_, _) => await RefreshAsync(userInitiated: true);
        _infoLabel = new Label { AutoSize = true, Margin = new Padding(0, 6, 0, 0), ForeColor = Color.DimGray };

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, Padding = new Padding(8, 6, 8, 4) };
        toolbar.Controls.Add(_refreshButton);
        toolbar.Controls.Add(_infoLabel);
        Controls.Add(toolbar);

        Grid.CellDoubleClick += Grid_CellDoubleClick;
        LoadRows();

        FormClosing += (_, _) => _cancellation.Cancel();
        Shown += async (_, _) =>
        {
            var stale = _data.UpdatedAt is not { } updated || DateTime.Now - updated > StaleAfter;
            if (_data.Bonds.Count == 0 || stale)
            {
                await RefreshAsync(userInitiated: false);
            }
        };
    }

    private void Highlight(DataGridViewColumn column, Color headerColor, Color cellColor)
    {
        column.HeaderCell.Style.BackColor = headerColor;
        column.HeaderCell.Style.Font = new Font(Font, FontStyle.Bold);
        column.DefaultCellStyle.BackColor = cellColor;
        column.DefaultCellStyle.Font = new Font(Font, FontStyle.Bold);
    }

    private async Task RefreshAsync(bool userInitiated)
    {
        if (_refreshing)
        {
            return;
        }

        _refreshing = true;
        _refreshButton.Enabled = false;
        _infoLabel.ForeColor = Color.DimGray;
        _infoLabel.Text = "Aggiornamento da Borsa Italiana in corso...";

        try
        {
            var result = await BtpOnlineProvider.FetchAsync(_cancellation.Token);
            if (IsDisposed)
            {
                return;
            }

            _data.Bonds = result.Bonds;
            _data.UpdatedAt = DateTime.Now;
            _data.Source = BtpOnlineProvider.SourceName;
            _skippedNoPrice = result.SkippedNoPrice;
            _unrecognized = result.Unrecognized;
            BtpStore.Save(_data);
            LoadRows();
        }
        catch (OperationCanceledException)
        {
            // Finestra chiusa durante l'aggiornamento: niente da mostrare.
        }
        catch (Exception ex)
        {
            if (IsDisposed)
            {
                return;
            }

            LoadRows();
            _infoLabel.ForeColor = Color.Firebrick;
            _infoLabel.Text = _data.Bonds.Count > 0
                ? $"Aggiornamento non riuscito ({ex.Message}) — mostro gli ultimi dati salvati."
                : $"Aggiornamento non riuscito ({ex.Message}).";

            if (userInitiated || _data.Bonds.Count == 0)
            {
                MessageBox.Show(this, $"Impossibile scaricare i BTP da Borsa Italiana:\n{ex.Message}", "Elenco BTP",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        finally
        {
            _refreshing = false;
            if (!IsDisposed)
            {
                _refreshButton.Enabled = true;
            }
        }
    }

    private void LoadRows()
    {
        var settlement = BondMath.AddWorkdays(DateOnly.FromDateTime(DateTime.Today), 2);
        var tax = _data.Inputs.TaxRate;

        var rows = new List<Row>();
        foreach (var bond in _data.Bonds.Where(b => b.Maturity > settlement))
        {
            var context = BtpCalculator.CreateContext(bond,
                new BtpParams(_data.Inputs.Capital, _data.Inputs.Commission, tax, _data.Inputs.Lot, settlement));
            if (context is null)
            {
                continue;
            }

            double? gross = null;
            double? net = null;
            if (bond.Price > 0)
            {
                var rate = (double)bond.Coupon;
                var price = (double)bond.Price;
                var g = BondMath.Yield(settlement, bond.Maturity, rate, price, 100);
                var n = BondMath.Yield(settlement, bond.Maturity, rate * (1 - (double)tax), price,
                    100 - Math.Max(0, 100 - price) * (double)tax);
                gross = double.IsNaN(g) ? null : g;
                net = double.IsNaN(n) ? null : n;
            }

            rows.Add(new Row(bond.Isin, bond.Description, bond.Coupon, bond.Maturity.ToDateTime(TimeOnly.MinValue),
                bond.Price, context.YearsRemaining, context.AccruedPer100,
                bond.Price > 0 ? bond.Coupon * 100m / bond.Price : 0m, gross, net));
        }

        Grid.DataSource = rows;

        var skippedNote = _skippedNoPrice > 0 ? $" ({_skippedNoPrice} senza prezzo esclusi)" : "";
        var unrecognizedNote = _unrecognized.Count > 0
            ? $" — ATTENZIONE: {_unrecognized.Count} titoli non riconosciuti ({string.Join(", ", _unrecognized.Take(3))}{(_unrecognized.Count > 3 ? ", ..." : "")}): potrebbero essere BTP con un nuovo formato di nome"
            : "";
        _infoLabel.ForeColor = _unrecognized.Count > 0 ? Color.DarkOrange : Color.DimGray;
        _infoLabel.Text = _data.UpdatedAt is { } updated
            ? string.Create(ItCulture,
                $"{rows.Count} BTP a tasso fisso — {_data.Source} — aggiornati il {updated:dd/MM/yyyy HH:mm}{skippedNote}{unrecognizedNote}")
            : "Nessun dato scaricato.";
        SetStatus(string.Create(ItCulture,
            $"Regolamento {settlement:dd/MM/yyyy} (T+2), imposta {tax:P1} — doppio click su un BTP per calcolare capitale, cedole e surplus."));
    }

    private void Grid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || Grid.Rows[e.RowIndex].DataBoundItem is not Row row)
        {
            return;
        }

        var bond = _data.Bonds.FirstOrDefault(b => b.Isin == row.Isin);
        if (bond is null)
        {
            return;
        }

        using var prompt = new CapitalPromptForm(bond.Description, _data.Inputs.Capital);
        if (prompt.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _data.Inputs.Capital = prompt.Capital;
        BtpStore.Save(_data);

        var calculator = new BtpCalculatorForm(bond, _data) { MdiParent = MdiParent };
        calculator.Show();
    }
}

using DPOper.Models;
using DPOper.Services;

namespace DPOper.Forms;

/// <summary>
/// Calcolatore per un singolo BTP, come il foglio Calcolatore_BTP.xlsx: dato il capitale (chiesto prima
/// di aprire la finestra) mostra quanti tagli si comprano, la cedola lorda incassata, il confronto con
/// lo stesso acquisto "se il BTP quotasse 100" e il surplus a scadenza. Tutti gli input restano
/// modificabili in alto e il calcolo si aggiorna subito.
/// </summary>
public sealed class BtpCalculatorForm : GridFormBase
{
    private enum Kind { Price, Money, Count, Percent }

    private sealed record Row(string Voce, string Market, string At100, string Diff, string Note, bool Highlight);

    private readonly BtpBond _bond;
    private readonly BtpData _data;

    private readonly NumericUpDown _capital;
    private readonly NumericUpDown _commission;
    private readonly NumericUpDown _taxPercent;
    private readonly NumericUpDown _lot;
    private readonly DateTimePicker _settlement;
    private readonly Label _infoLabel;
    private readonly Label _summaryLabel;
    private readonly Font _boldFont;

    public BtpCalculatorForm(BtpBond bond, BtpData data)
        : base("BtpCalculatorForm", $"Calcolatore BTP - {bond.Description}", 1000, 800)
    {
        _bond = bond;
        _data = data;
        _boldFont = new Font(Font, FontStyle.Bold);

        AddColumn("Voce", "Voce", null, DataGridViewContentAlignment.MiddleLeft, 300);
        AddColumn("Market", "Prezzo di mercato", null, DataGridViewContentAlignment.MiddleRight, 120);
        AddColumn("At100", "Se quotasse 100", null, DataGridViewContentAlignment.MiddleRight, 120);
        AddColumn("Diff", "Differenza", null, DataGridViewContentAlignment.MiddleRight, 110);
        var noteColumn = AddColumn("Note", "Nota", null, DataGridViewContentAlignment.MiddleLeft, 340);
        noteColumn.DefaultCellStyle.ForeColor = Color.DimGray;
        foreach (DataGridViewColumn column in Grid.Columns)
        {
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
        }
        Grid.CellFormatting += Grid_CellFormatting;
        HideTotalsFooter();

        var inputs = data.Inputs;
        _capital = Numeric(0, 1_000_000_000, 1000, 0, inputs.Capital, 130);
        _commission = Numeric(0, 10_000, 1, 2, inputs.Commission, 80);
        _taxPercent = Numeric(0, 100, 0.5m, 1, inputs.TaxRate * 100m, 70);
        _lot = Numeric(1, 1_000_000, 1000, 0, inputs.Lot, 90);
        _settlement = new DateTimePicker
        {
            Format = DateTimePickerFormat.Short,
            Value = BondMath.AddWorkdays(DateOnly.FromDateTime(DateTime.Today), 2).ToDateTime(TimeOnly.MinValue),
            Width = 110,
        };

        var inputPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(8, 8, 8, 4),
        };
        AddInput(inputPanel, "Capitale da investire (€):", _capital);
        AddInput(inputPanel, "Commissioni (€):", _commission);
        AddInput(inputPanel, "Aliquota (%):", _taxPercent);
        AddInput(inputPanel, "Taglio minimo (€):", _lot);
        AddInput(inputPanel, "Regolamento:", _settlement);

        _infoLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 62,
            Padding = new Padding(10, 4, 10, 0),
            ForeColor = Color.DimGray,
        };
        _summaryLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(10, 6, 10, 4),
            Font = _boldFont,
        };

        // L'ultimo controllo aggiunto con Dock=Top finisce più in alto: inputs, poi dati del titolo, poi sintesi.
        Controls.Add(_summaryLabel);
        Controls.Add(_infoLabel);
        Controls.Add(inputPanel);

        foreach (var control in new Control[] { _capital, _commission, _taxPercent, _lot })
        {
            ((NumericUpDown)control).ValueChanged += (_, _) => Recalculate();
        }
        _settlement.ValueChanged += (_, _) => Recalculate();
        FormClosing += (_, _) => SaveInputs();

        Recalculate();
    }

    private static NumericUpDown Numeric(decimal min, decimal max, decimal increment, int decimals, decimal value, int width) => new()
    {
        Minimum = min,
        Maximum = max,
        Increment = increment,
        DecimalPlaces = decimals,
        ThousandsSeparator = true,
        Value = Math.Clamp(value, min, max),
        Width = width,
    };

    private static void AddInput(FlowLayoutPanel panel, string label, Control control)
    {
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
        control.Margin = new Padding(0, 2, 18, 0);
        panel.Controls.Add(control);
    }

    private void SaveInputs()
    {
        _data.Inputs.Capital = _capital.Value;
        _data.Inputs.Commission = _commission.Value;
        _data.Inputs.TaxRate = _taxPercent.Value / 100m;
        _data.Inputs.Lot = _lot.Value;
        BtpStore.Save(_data);
    }

    private void Recalculate()
    {
        var parameters = new BtpParams(_capital.Value, _commission.Value, _taxPercent.Value / 100m, _lot.Value,
            DateOnly.FromDateTime(_settlement.Value));
        var context = BtpCalculator.CreateContext(_bond, parameters);

        if (context is null)
        {
            _infoLabel.Text = string.Create(ItCulture, $"{_bond.Isin}  {_bond.Description}: scade il {_bond.Maturity:dd/MM/yyyy}.");
            _summaryLabel.Text = "Il titolo è già scaduto alla data di regolamento scelta: cambia la data per il calcolo.";
            Grid.DataSource = new List<Row>();
            SetStatus("Nessun calcolo possibile.");
            return;
        }

        var market = BtpCalculator.Compute(context, _bond.Price);
        var at100 = BtpCalculator.Compute(context, 100m);
        var taxLabel = string.Create(ItCulture, $"{_taxPercent.Value:0.##}%");

        var priceTimeNote = _data.UpdatedAt is { } priceTime
            ? string.Create(ItCulture, $"  |  prezzo Borsa Italiana del {priceTime:dd/MM/yyyy HH:mm}")
            : "";
        _infoLabel.Text = string.Create(ItCulture,
            $"{_bond.Isin}  {_bond.Description}  |  cedola lorda {_bond.Coupon:0.0##%}  |  scadenza {_bond.Maturity:dd/MM/yyyy}  |  " +
            $"corso secco {_bond.Price:0.00#}  |  rateo maturato {context.AccruedPer100:0.####} per 100  |  " +
            $"prezzo tel quel {_bond.Price + context.AccruedPer100:0.00##}  |  prossimo stacco {context.Coupon.Next:dd/MM/yyyy}  |  " +
            $"cedole semestrali ancora da incassare {context.Coupon.Remaining}  |  anni residui {context.YearsRemaining:0.00}{priceTimeNote}");

        var couponGain = market.AnnualCoupon - at100.AnnualCoupon;
        _summaryLabel.Text = string.Create(ItCulture,
            $"Con {_capital.Value:N0} € compri {market.Nominal:N0} € nominali di {_bond.Description}: incassi " +
            $"{market.AnnualCoupon:N2} € lordi di cedola l'anno ({(couponGain >= 0 ? "+" : "")}{couponGain:N2} € rispetto al prezzo 100) " +
            $"e a scadenza hai un surplus di {market.Surplus:N2} €.");

        var rows = new List<Row>
        {
            Line("Prezzo secco pagato (per 100)", Kind.Price, market.Price, at100.Price),
            Line("Costo di un taglio minimo (tel quel)", Kind.Money, market.LotCost, at100.LotCost, "Prezzo + rateo, applicati al lotto minimo."),
            Line("Numero di tagli acquistabili", Kind.Count, market.Lots, at100.Lots, "Quanti lotti da taglio minimo ci stanno nel capitale, al netto delle commissioni."),
            Line("Valore nominale acquistato (= rimborso a scadenza)", Kind.Money, market.Nominal, at100.Nominal, "È quanto lo Stato ti restituisce a scadenza."),
            Line("Controvalore al prezzo secco", Kind.Money, market.CleanValue, at100.CleanValue),
            Line("Rateo pagato al venditore", Kind.Money, market.AccruedPaid, at100.AccruedPaid, "Lo recuperi interamente con la prima cedola."),
            Line("Commissioni", Kind.Money, market.Commission, at100.Commission),
            Line("Esborso totale", Kind.Money, market.TotalOutlay, at100.TotalOutlay, highlight: true),
            Line("Liquidità che avanza", Kind.Money, market.LeftoverCash, at100.LeftoverCash),
            Line("Cedola semestrale lorda", Kind.Money, market.SemiannualCoupon, at100.SemiannualCoupon),
            Line("Cedola annua lorda incassata", Kind.Money, market.AnnualCoupon, at100.AnnualCoupon, highlight: true),
            Line($"Cedola annua netta (dopo {taxLabel})", Kind.Money, market.AnnualCouponNet, at100.AnnualCouponNet),
            Line("Rendimento cedolare lordo sul capitale investito", Kind.Percent, market.CouponYield, at100.CouponYield, "Cedola annua / controvalore secco. A 100 coincide con il tasso nominale."),
            Line("Totale cedole lorde fino a scadenza", Kind.Money, market.TotalCoupons, at100.TotalCoupons, "Include la prima cedola intera (che restituisce anche il rateo pagato)."),
            Line("SURPLUS a scadenza (rimborso - prezzo secco)", Kind.Money, market.Surplus, at100.Surplus, "Positivo = capital gain (titolo sotto la pari). Negativo = perdita in conto capitale (titolo sopra la pari).", highlight: true),
            Line($"Imposta sul surplus ({taxLabel} se positivo)", Kind.Money, market.SurplusTax, at100.SurplusTax, "Se il surplus è negativo genera una minusvalenza compensabile (non calcolata qui)."),
            Line("Surplus netto a scadenza", Kind.Money, market.SurplusNet, at100.SurplusNet),
            Line("Surplus annualizzato sul capitale investito", Kind.Percent, market.SurplusAnnualized, at100.SurplusAnnualized, "Quanto il recupero verso 100 aggiunge ogni anno al rendimento."),
            Line("Guadagno totale lordo a scadenza", Kind.Money, market.GrossGain, at100.GrossGain, "Cedole + rimborso - esborso (rateo e commissioni inclusi)."),
            Line("Guadagno totale netto a scadenza", Kind.Money, market.NetGain, at100.NetGain, $"Stima semplificata: tassazione al {taxLabel} su tutte le cedole e sul surplus.", highlight: true),
            Line("Rendimento effettivo a scadenza lordo (annuo)", Kind.Percent, ToDecimal(market.GrossYield), ToDecimal(at100.GrossYield), "Tasso interno di rendimento (formula RENDIMENTO di Excel), cedole semestrali."),
            Line("Rendimento effettivo a scadenza netto (annuo)", Kind.Percent, ToDecimal(market.NetYield), ToDecimal(at100.NetYield), "Approssimazione: cedole nette e rimborso al netto dell'imposta sul capital gain."),
        };

        Grid.DataSource = rows;
        SetStatus(string.Create(ItCulture,
            $"Regolamento {_settlement.Value:dd/MM/yyyy} — rispetto al prezzo 100, a parità di capitale: " +
            $"{market.Lots - at100.Lots:+0;-0;0} tagli, surplus a scadenza {market.Surplus:N2} €."));
    }

    private static decimal? ToDecimal(double? value) => value is null ? null : (decimal)value.Value;

    private static Row Line(string label, Kind kind, decimal? market, decimal? at100, string note = "", bool highlight = false)
    {
        var diff = market is { } m && at100 is { } c ? m - c : (decimal?)null;
        return new Row(label, Format(kind, market), Format(kind, at100), FormatDiff(kind, diff), note, highlight);
    }

    private static string Format(Kind kind, decimal? value)
    {
        if (value is not { } v)
        {
            return "n/d";
        }

        return kind switch
        {
            Kind.Price => v.ToString("0.00#", ItCulture),
            Kind.Money => v.ToString("#,##0.00 €", ItCulture),
            Kind.Count => v.ToString("N0", ItCulture),
            _ => v.ToString("P2", ItCulture),
        };
    }

    private static string FormatDiff(Kind kind, decimal? value)
    {
        if (value is not { } v)
        {
            return "n/d";
        }

        return kind switch
        {
            Kind.Price => v.ToString("+0.00#;-0.00#;0.00", ItCulture),
            Kind.Money => v.ToString("+#,##0.00 €;-#,##0.00 €;0.00 €", ItCulture),
            Kind.Count => v.ToString("+0;-0;0", ItCulture),
            _ => v.ToString("+0.00%;-0.00%;0.00%", ItCulture),
        };
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || Grid.Rows[e.RowIndex].DataBoundItem is not Row { Highlight: true })
        {
            return;
        }

        e.CellStyle!.Font = _boldFont;
        e.CellStyle.BackColor = Color.FromArgb(255, 250, 225);
    }
}

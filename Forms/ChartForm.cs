using Pelago.Models;
using Pelago.Services;

namespace Pelago.Forms;

/// <summary>
/// Mostra l'andamento nel tempo di disponibilità, titoli, obbligazioni, crypto e totale,
/// sia come grafico a linee giornaliero sia come colonne impilate aggregate per mese
/// (una colonna al giorno sarebbe illeggibile su un anno di dati), più una vista di composizione
/// a passo variabile (giornaliero, settimanale, mensile, ... YTD).
/// </summary>
public sealed class ChartForm : Form
{
    public ChartForm(List<DailySnapshot> snapshots, List<Operation> operations,
        IReadOnlyDictionary<string, List<(DateTime Timestamp, decimal Price)>> quotes,
        DateOnly minDate, DateOnly maxDate)
    {
        Font = new Font("Consolas", 9f);
        Text = "Grafico";
        ClientSize = new Size(1000, 620);
        MinimumSize = new Size(600, 400);
        ShowIcon = false;

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildLineTab(snapshots));
        tabs.TabPages.Add(BuildStackedColumnTab(snapshots));
        tabs.TabPages.Add(BuildGapColumnTab(snapshots));
        tabs.TabPages.Add(BuildCompositionColumnTab(operations, quotes, minDate, maxDate));

        Controls.Add(tabs);

        FormLayoutPersistence.Attach(this, "ChartForm");
    }

    private static TabPage BuildLineTab(List<DailySnapshot> snapshots)
    {
        var series = new List<LineChartControl.SeriesDef>
        {
            new("Disponibilità", Color.SteelBlue, s => s.Cash),
            new("Titoli", Color.DarkOrange, s => s.Securities),
            new("Obbligazioni", Color.MediumSeaGreen, s => s.Bonds),
            new("Crypto", Color.MediumPurple, s => s.Crypto),
            new("Totale", Color.Black, s => s.Total),
        };

        var chart = new LineChartControl { Dock = DockStyle.Fill };
        chart.SetData(snapshots, series);

        var page = new TabPage("Andamento (linee)");
        page.Controls.Add(chart);
        page.Controls.Add(BuildLegendCheckboxes(series.Select(s => (s.Name, s.Color)).ToList(),
            (name, visible) => chart.SetSeriesVisible(name, visible)));
        return page;
    }

    private static TabPage BuildStackedColumnTab(List<DailySnapshot> snapshots)
    {
        var series = new List<StackedColumnChartControl.SeriesDef>
        {
            new("Disponibilità", Color.SteelBlue, s => s.Cash),
            new("Titoli", Color.DarkOrange, s => s.Securities),
            new("Obbligazioni", Color.MediumSeaGreen, s => s.Bonds),
            new("Crypto", Color.MediumPurple, s => s.Crypto),
        };

        var monthly = snapshots
            .GroupBy(s => new { s.Date.Year, s.Date.Month })
            .Select(g => g.OrderBy(s => s.Date).Last())
            .OrderBy(s => s.Date)
            .ToList();

        var chart = new StackedColumnChartControl { Dock = DockStyle.Fill };
        chart.SetData(monthly, series);

        var page = new TabPage("Composizione mensile (colonne impilate)");
        page.Controls.Add(chart);
        page.Controls.Add(BuildLegendCheckboxes(series.Select(s => (s.Name, s.Color)).ToList(),
            (name, visible) => chart.SetSeriesVisible(name, visible)));

        var note = new Label
        {
            Dock = DockStyle.Top,
            Height = 20,
            Padding = new Padding(10, 2, 0, 0),
            ForeColor = Color.DimGray,
            Font = new Font(page.Font, FontStyle.Italic),
            Text = $"Ogni colonna è l'ultimo giorno registrato di ogni mese ({monthly.Count} mesi).",
        };
        page.Controls.Add(note);

        return page;
    }

    private static TabPage BuildGapColumnTab(List<DailySnapshot> snapshots)
    {
        var monthly = snapshots
            .GroupBy(s => new { s.Date.Year, s.Date.Month })
            .Select(g => g.OrderBy(s => s.Date).Last())
            .OrderBy(s => s.Date)
            .ToList();

        var chart = new GapColumnChartControl { Dock = DockStyle.Fill };
        chart.SetData(monthly);

        var page = new TabPage("Sopra/sotto i versamenti");
        page.Controls.Add(chart);
        page.Controls.Add(BuildGapNote(page.Font,
            $"Colonna = Totale (base blu fino ai versamenti, cappello verde/rosso = scostamento) — un mese per colonna ({monthly.Count} mesi). " +
            "Linea tratteggiata = versamenti netti (gli interessi non contano come versamento, restano nel Totale)."));

        return page;
    }

    private static TabPage BuildCompositionColumnTab(
        List<Operation> operations,
        IReadOnlyDictionary<string, List<(DateTime Timestamp, decimal Price)>> quotes,
        DateOnly minDate,
        DateOnly maxDate)
    {
        var page = new TabPage("Composizione giornaliera");

        var chart = new CompositionColumnChartControl();
        var scrollPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        scrollPanel.Controls.Add(chart);

        var noteLabel = BuildGapNote(page.Font, "");

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36, Padding = new Padding(10, 6, 0, 0) };
        var periodLabel = new Label { Text = "Periodo:", AutoSize = true, Margin = new Padding(0, 6, 6, 0) };
        var periodCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
        foreach (var (_, label) in PeriodPointsGenerator.Options)
        {
            periodCombo.Items.Add(label);
        }
        periodCombo.SelectedIndex = 0;
        toolbar.Controls.Add(periodLabel);
        toolbar.Controls.Add(periodCombo);

        var pointCount = 0;

        void Relayout()
        {
            const int minPxPerColumn = 6;
            var neededWidth = Math.Max(1, pointCount) * minPxPerColumn;
            var availableWidth = scrollPanel.ClientSize.Width;
            chart.Width = Math.Max(availableWidth, neededWidth);
            chart.Height = Math.Max(1, scrollPanel.ClientSize.Height);
            chart.Location = new Point(0, 0);
            scrollPanel.PerformLayout();
            scrollPanel.HorizontalScroll.Value = scrollPanel.HorizontalScroll.Maximum;
        }

        void RefreshChart()
        {
            var selected = PeriodPointsGenerator.Options[periodCombo.SelectedIndex].Type;
            var dates = PeriodPointsGenerator.Generate(maxDate, minDate, selected);
            var points = dates.Select(d => PortfolioCalculator.ComputeAsOf(operations, d, quotes)).ToList();
            pointCount = points.Count;
            chart.SetData(points);
            noteLabel.Text = $"{points.Count} colonne — colori come in \"Composizione ad una data\"; linea tratteggiata = versamenti netti; " +
                              "tinta verde/rossa = scostamento sopra/sotto (percentuale nel tooltip al passaggio del mouse).";
            Relayout();
        }

        periodCombo.SelectedIndexChanged += (_, _) => RefreshChart();
        scrollPanel.Resize += (_, _) => Relayout();

        page.Controls.Add(scrollPanel);
        page.Controls.Add(noteLabel);
        page.Controls.Add(toolbar);

        RefreshChart();

        return page;
    }

    private static Label BuildGapNote(Font baseFont, string text) => new()
    {
        Dock = DockStyle.Top,
        Height = 20,
        Padding = new Padding(10, 2, 0, 0),
        ForeColor = Color.DimGray,
        Font = new Font(baseFont, FontStyle.Italic),
        Text = text,
    };

    private static FlowLayoutPanel BuildLegendCheckboxes(List<(string Name, Color Color)> items,
        Action<string, bool> onVisibilityChanged)
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 32,
            Padding = new Padding(10, 8, 8, 0),
        };

        foreach (var (name, color) in items)
        {
            var checkBox = new CheckBox
            {
                Text = name,
                Checked = true,
                AutoSize = true,
                ForeColor = color,
                Font = new Font(panel.Font, FontStyle.Bold),
                Margin = new Padding(0, 0, 16, 0),
            };
            checkBox.CheckedChanged += (_, _) => onVisibilityChanged(name, checkBox.Checked);
            panel.Controls.Add(checkBox);
        }

        return panel;
    }
}

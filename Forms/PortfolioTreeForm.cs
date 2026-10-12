using System.ComponentModel;
using System.Globalization;
using DPOper.Models;
using DPOper.Services;

namespace DPOper.Forms;

/// <summary>
/// Composizione del conto a una data scelta: a sinistra una checkbox per ciascuna categoria
/// (Saldo disponibile, Crypto, Obbligazioni, Azioni, Fondi, Derivati, Interessi); selezionandone
/// una se ne aggiungono le righe alla griglia a destra, selezionandone altre si accumulano — righe
/// colorate per categoria (stesso colore della checkbox) per distinguerle quando sono mescolate.
/// Navigazione nel tempo tramite calendario o giorno prec./succ.: muovendosi nel tempo
/// compaiono/spariscono asset comprati/chiusi in date diverse, come atteso.
/// </summary>
public sealed class PortfolioTreeForm : Form
{
    private static readonly CultureInfo ItCulture = CultureInfo.GetCultureInfo("it-IT");

    private static readonly (string Key, string Label, Color Color)[] Categories = AssetClassPalette.Categories;

    private readonly List<Operation> _operations;
    private readonly IReadOnlyDictionary<string, List<(DateTime Timestamp, decimal Price)>> _quotes;
    private readonly DateOnly _minDate;
    private readonly DateOnly _maxDate;

    private readonly DateTimePicker _datePicker;
    private readonly Dictionary<string, CheckBox> _checkboxes = new();
    private readonly HashSet<string> _checkedCategories = new();
    private readonly DataGridView _grid;
    private readonly ToolStripStatusLabel _statusLabel;

    private PortfolioAsOf? _current;

    public PortfolioTreeForm(List<Operation> operations, DateOnly minDate, DateOnly maxDate,
        IReadOnlyDictionary<string, List<(DateTime Timestamp, decimal Price)>> quotes)
    {
        _operations = operations;
        _quotes = quotes;
        _minDate = minDate;
        _maxDate = maxDate;

        Font = new Font("Consolas", 9f);
        Text = "Composizione ad una data";
        ClientSize = new Size(950, 620);
        MinimumSize = new Size(650, 380);
        ShowIcon = false;

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(8, 6, 8, 6) };

        var prevButton = new Button { Text = "◀ Giorno prec.", AutoSize = true };
        var nextButton = new Button { Text = "Giorno succ. ▶", AutoSize = true };
        var todayButton = new Button { Text = "Oggi", AutoSize = true, Margin = new Padding(16, 0, 0, 0) };

        _datePicker = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dddd dd MMMM yyyy",
            MinDate = minDate.ToDateTime(TimeOnly.MinValue),
            MaxDate = maxDate.ToDateTime(TimeOnly.MinValue),
            Value = maxDate.ToDateTime(TimeOnly.MinValue),
            Width = 220,
        };

        prevButton.Click += (_, _) => StepDate(-1);
        nextButton.Click += (_, _) => StepDate(1);
        todayButton.Click += (_, _) => SetDate(_maxDate);
        _datePicker.ValueChanged += (_, _) => RefreshData(DateOnly.FromDateTime(_datePicker.Value));

        toolbar.Controls.Add(prevButton);
        toolbar.Controls.Add(_datePicker);
        toolbar.Controls.Add(nextButton);
        toolbar.Controls.Add(todayButton);

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 220, FixedPanel = FixedPanel.Panel1 };

        var checkPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(10, 10, 0, 0),
        };
        foreach (var (key, label, color) in Categories)
        {
            var checkBox = new CheckBox
            {
                Text = label,
                AutoSize = true,
                ForeColor = color,
                Font = new Font(Font, FontStyle.Bold),
                Tag = key,
                Margin = new Padding(0, 0, 0, 10),
            };
            checkBox.CheckedChanged += (_, _) => ToggleCategory(key, checkBox.Checked);
            checkPanel.Controls.Add(checkBox);
            _checkboxes[key] = checkBox;
        }

        split.Panel1.Controls.Add(checkPanel);

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoGenerateColumns = false,
            BackgroundColor = SystemColors.Window,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            Name = "Grid",
        };
        BuildColumns();
        _grid.CellDoubleClick += Grid_CellDoubleClick;
        _grid.CellFormatting += Grid_CellFormatting;
        _grid.ColumnHeaderMouseClick += Grid_ColumnHeaderMouseClick;
        split.Panel2.Controls.Add(_grid);

        var statusStrip = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel();
        statusStrip.Items.Add(_statusLabel);

        Controls.Add(split);
        Controls.Add(statusStrip);
        Controls.Add(toolbar);

        FormLayoutPersistence.Attach(this, "PortfolioTreeForm", _grid);

        RefreshData(maxDate);
        _statusLabel.Text = "Seleziona una o più voci a sinistra per vederne il dettaglio.";
    }

    private void BuildColumns()
    {
        _grid.Columns.Add(TextColumn("Categoria", "Categoria", null, DataGridViewContentAlignment.MiddleLeft, 110));
        _grid.Columns.Add(TextColumn("Name", "Nome", null, DataGridViewContentAlignment.MiddleLeft, 220));
        _grid.Columns.Add(TextColumn("Symbol", "Simbolo", null, DataGridViewContentAlignment.MiddleLeft, 90));
        _grid.Columns.Add(TextColumn("Shares", "Quantità", "N4", DataGridViewContentAlignment.MiddleRight, 100));
        _grid.Columns.Add(TextColumn("Price", "Prezzo", "C4", DataGridViewContentAlignment.MiddleRight, 100));
        _grid.Columns.Add(TextColumn("Value", "Valore", "C2", DataGridViewContentAlignment.MiddleRight, 100));
    }

    private static DataGridViewTextBoxColumn TextColumn(string name, string header, string? format,
        DataGridViewContentAlignment alignment, float fillWeight) => new()
    {
        Name = name,
        DataPropertyName = name,
        HeaderText = header,
        AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
        FillWeight = fillWeight,
        SortMode = DataGridViewColumnSortMode.Programmatic,
        DefaultCellStyle = new DataGridViewCellStyle { Format = format, FormatProvider = ItCulture, Alignment = alignment },
    };

    private void StepDate(int days) => SetDate(DateOnly.FromDateTime(_datePicker.Value).AddDays(days));

    private void SetDate(DateOnly date)
    {
        if (date < _minDate)
        {
            date = _minDate;
        }
        if (date > _maxDate)
        {
            date = _maxDate;
        }
        _datePicker.Value = date.ToDateTime(TimeOnly.MinValue);
        // Se la data non cambia (già al limite), ValueChanged non scatta: aggiorniamo comunque.
        RefreshData(date);
    }

    private void RefreshData(DateOnly date)
    {
        _current = PortfolioCalculator.ComputeAsOf(_operations, date, _quotes);

        foreach (var (key, label, _) in Categories)
        {
            _checkboxes[key].Text = key switch
            {
                "CASH" => $"{label} ({_current.Cash:C2})",
                "INTEREST" => BuildInterestLabel(label, date),
                _ => BuildCategoryLabel(label, key),
            };
        }

        RebuildGrid();

        string BuildCategoryLabel(string label, string assetClass)
        {
            var assets = _current.Assets.Where(a => a.AssetClass == assetClass).ToList();
            return $"{label} ({assets.Count} — {assets.Sum(a => a.Value):C2})";
        }

        string BuildInterestLabel(string label, DateOnly asOf)
        {
            var ops = InterestUpTo(asOf);
            return $"{label} ({ops.Count} — {ops.Sum(o => o.NetCashImpact):C2})";
        }
    }

    private List<Operation> InterestUpTo(DateOnly date)
    {
        var endOfDay = date.ToDateTime(TimeOnly.MaxValue);
        return _operations.Where(o => o.Type == "INTEREST_PAYMENT" && o.DateTime <= endOfDay)
            .OrderByDescending(o => o.DateTime).ToList();
    }

    private void ToggleCategory(string key, bool isChecked)
    {
        if (isChecked)
        {
            _checkedCategories.Add(key);
        }
        else
        {
            _checkedCategories.Remove(key);
        }
        RebuildGrid();
    }

    private void RebuildGrid()
    {
        if (_current is null)
        {
            return;
        }

        var rows = new List<CombinedRow>();
        var asOf = DateOnly.FromDateTime(_datePicker.Value);

        foreach (var (key, label, _) in Categories)
        {
            if (!_checkedCategories.Contains(key))
            {
                continue;
            }

            switch (key)
            {
                case "CASH":
                    rows.Add(new CombinedRow(label, "Saldo disponibile", "", null, null, _current.Cash, ""));
                    break;

                case "INTEREST":
                    foreach (var op in InterestUpTo(asOf))
                    {
                        rows.Add(new CombinedRow(label, $"Interesse del {op.DateTime.ToLocalTime():dd/MM/yyyy}",
                            "", null, null, op.NetCashImpact, ""));
                    }
                    break;

                default:
                    foreach (var asset in _current.Assets.Where(a => a.AssetClass == key))
                    {
                        rows.Add(new CombinedRow(label, asset.Name, asset.Symbol, asset.Shares, asset.Price, asset.Value, asset.Key));
                    }
                    break;
            }
        }

        _grid.DataSource = rows;

        _statusLabel.Text = rows.Count == 0
            ? "Seleziona una o più voci a sinistra per vederne il dettaglio."
            : $"{rows.Count} righe — Totale selezione: {rows.Sum(r => r.Value):C2}   " +
              $"(Composizione al {asOf:dd/MM/yyyy} — Totale conto: {_current.Total:C2})";
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (_grid.Rows[e.RowIndex].DataBoundItem is not CombinedRow row)
        {
            return;
        }

        var color = Categories.First(c => c.Label == row.Categoria).Color;
        e.CellStyle!.BackColor = Lighten(color);
    }

    private static Color Lighten(Color color, double amount = 0.8) => Color.FromArgb(
        (int)(color.R + (255 - color.R) * amount),
        (int)(color.G + (255 - color.G) * amount),
        (int)(color.B + (255 - color.B) * amount));

    private void Grid_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        var column = _grid.Columns[e.ColumnIndex];
        var direction = column.HeaderCell.SortGlyphDirection == SortOrder.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;
        DataGridViewSorter.SortBy(_grid, column, direction);
    }

    private void Grid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || _grid.Rows[e.RowIndex].DataBoundItem is not CombinedRow row
            || string.IsNullOrEmpty(row.Key))
        {
            return;
        }

        var asOf = DateOnly.FromDateTime(_datePicker.Value);
        var endOfDay = asOf.ToDateTime(TimeOnly.MaxValue);
        var history = _operations.Where(o => o.PositionKey == row.Key && o.DateTime <= endOfDay).ToList();
        if (history.Count == 0)
        {
            return;
        }

        var detail = new SecurityDetailForm(row.Name, history) { MdiParent = MdiParent };
        detail.Show();
    }

    private sealed record CombinedRow(string Categoria, string Name, string Symbol, decimal? Shares, decimal? Price, decimal Value, string Key);
}

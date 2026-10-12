using System.ComponentModel;
using System.Globalization;
using DPOper.Services;

namespace DPOper.Forms;

/// <summary>
/// Base comune per le finestre di dettaglio: una griglia a tutta finestra (ordinabile, con colonne
/// riordinabili/ridimensionabili — l'ordine, la larghezza e l'ordinamento vengono ricordati tra un
/// riavvio e l'altro tramite <see cref="FormLayoutPersistence"/>), una barra di totale in fondo
/// sempre visibile (non fa parte dei dati quindi non viene mai spostata da un ordinamento) e una
/// riga di stato. Font Consolas come nel resto della suite, stesso accorgimento di WSDoc24Tools.
/// </summary>
public class GridFormBase : Form
{
    protected static readonly CultureInfo ItCulture = CultureInfo.GetCultureInfo("it-IT");
    private static readonly Font AppFont = new("Consolas", 9f);

    protected DataGridView Grid { get; }
    private readonly DataGridView _footerGrid;
    private readonly StatusStrip _statusStrip;
    private readonly ToolStripStatusLabel _statusLabel;

    public GridFormBase(string persistenceKey, string title, int width = 900, int height = 550)
    {
        Font = AppFont;

        Grid = CreateGrid();
        Grid.Name = "Grid";
        Grid.Dock = DockStyle.Fill;
        Grid.AllowUserToOrderColumns = true;
        Grid.ColumnWidthChanged += (_, e) => MirrorColumn(e.Column);
        Grid.ColumnDisplayIndexChanged += (_, e) => MirrorColumn(e.Column);
        Grid.ColumnHeaderMouseClick += Grid_ColumnHeaderMouseClick;

        _footerGrid = CreateGrid();
        _footerGrid.Name = "FooterGrid";
        _footerGrid.Dock = DockStyle.Bottom;
        _footerGrid.Height = 26;
        _footerGrid.ColumnHeadersVisible = false;
        _footerGrid.ScrollBars = ScrollBars.None;
        _footerGrid.AllowUserToOrderColumns = false;
        _footerGrid.AllowUserToResizeColumns = false;
        _footerGrid.TabStop = false;
        _footerGrid.DefaultCellStyle.BackColor = Color.FromArgb(228, 228, 228);
        _footerGrid.DefaultCellStyle.Font = new Font(AppFont, FontStyle.Bold);
        _footerGrid.BackgroundColor = Color.FromArgb(228, 228, 228);

        _statusLabel = new ToolStripStatusLabel();
        _statusStrip = new StatusStrip();
        _statusStrip.Items.Add(_statusLabel);

        Controls.Add(Grid);
        Controls.Add(_footerGrid);
        Controls.Add(_statusStrip);

        Text = title;
        ClientSize = new Size(width, height);
        MinimumSize = new Size(500, 300);
        ShowIcon = false;

        FormLayoutPersistence.Attach(this, persistenceKey, Grid);
    }

    private static DataGridView CreateGrid() => new()
    {
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoGenerateColumns = false,
        BackgroundColor = SystemColors.Window,
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
        ReadOnly = true,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
    };

    protected DataGridViewTextBoxColumn AddColumn(
        string name,
        string header,
        string? format = null,
        DataGridViewContentAlignment alignment = DataGridViewContentAlignment.MiddleLeft,
        float fillWeight = 100f)
    {
        DataGridViewTextBoxColumn Build() => new()
        {
            Name = name,
            DataPropertyName = name,
            HeaderText = header,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = fillWeight,
            SortMode = DataGridViewColumnSortMode.Programmatic,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Format = format,
                FormatProvider = ItCulture,
                Alignment = alignment,
            },
        };

        var column = Build();
        Grid.Columns.Add(column);

        var footerColumn = Build();
        footerColumn.Resizable = DataGridViewTriState.False;
        footerColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
        _footerGrid.Columns.Add(footerColumn);

        return column;
    }

    /// <summary>
    /// Mostra una riga di totale fissa in fondo alla griglia, sempre visibile indipendentemente
    /// dall'ordinamento o dallo scroll della griglia principale. <paramref name="totalRow"/> deve
    /// essere dello stesso tipo delle righe di <see cref="Grid"/>.
    /// </summary>
    protected void SetTotals<T>(T totalRow) => _footerGrid.DataSource = new List<T> { totalRow };

    /// <summary>Nasconde la barra di totale per le griglie dove sommare le colonne non ha senso.</summary>
    protected void HideTotalsFooter()
    {
        _footerGrid.Visible = false;
        _footerGrid.Height = 0;
    }

    protected void SetStatus(string text) => _statusLabel.Text = text;

    private void Grid_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        var column = Grid.Columns[e.ColumnIndex];
        if (column.SortMode == DataGridViewColumnSortMode.NotSortable)
        {
            return;
        }

        var isCurrentlyAscending = column.HeaderCell.SortGlyphDirection == SortOrder.Ascending;
        var direction = isCurrentlyAscending ? ListSortDirection.Descending : ListSortDirection.Ascending;
        DataGridViewSorter.SortBy(Grid, column, direction);
    }

    private void MirrorColumn(DataGridViewColumn column)
    {
        if (!_footerGrid.Columns.Contains(column.Name))
        {
            return;
        }
        _footerGrid.Columns[column.Name].Width = column.Width;
        _footerGrid.Columns[column.Name].DisplayIndex = column.DisplayIndex;
    }
}

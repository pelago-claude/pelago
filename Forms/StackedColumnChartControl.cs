using System.Drawing.Drawing2D;
using System.Globalization;
using Pelago.Services;

namespace Pelago.Forms;

/// <summary>
/// Grafico a colonne impilate (senza dipendenze esterne): per ogni periodo, mostra la
/// composizione del conto come segmenti impilati di ogni serie, con crosshair/tooltip al
/// passaggio del mouse.
/// </summary>
public sealed class StackedColumnChartControl : Control
{
    public sealed record SeriesDef(string Name, Color Color, Func<DailySnapshot, decimal> Selector);

    private const int MarginLeft = 95;
    private const int MarginRight = 24;
    private const int MarginTop = 16;
    private const int MarginBottom = 32;

    private static readonly CultureInfo ItCulture = CultureInfo.GetCultureInfo("it-IT");
    private readonly Font _labelFont = new("Segoe UI", 8f);

    private List<DailySnapshot> _data = new();
    private List<SeriesDef> _allSeries = new();
    private HashSet<string> _visibleSeries = new();
    private int _hoverIndex = -1;

    public StackedColumnChartControl()
    {
        DoubleBuffered = true;
        BackColor = Color.White;
        MouseMove += OnMouseMove;
        MouseLeave += (_, _) => SetHover(-1);
    }

    public void SetData(List<DailySnapshot> data, List<SeriesDef> series)
    {
        _data = data.OrderBy(d => d.Date).ToList();
        _allSeries = series;
        _visibleSeries = series.Select(s => s.Name).ToHashSet();
        Invalidate();
    }

    public void SetSeriesVisible(string name, bool visible)
    {
        if (visible)
        {
            _visibleSeries.Add(name);
        }
        else
        {
            _visibleSeries.Remove(name);
        }
        Invalidate();
    }

    private Rectangle PlotRect => new(MarginLeft, MarginTop,
        Math.Max(1, Width - MarginLeft - MarginRight), Math.Max(1, Height - MarginTop - MarginBottom));

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        if (_data.Count == 0)
        {
            g.DrawString("Nessun dato da mostrare.", Font, Brushes.Gray, 10, 10);
            return;
        }

        var visible = _allSeries.Where(s => _visibleSeries.Contains(s.Name)).ToList();
        if (visible.Count == 0)
        {
            g.DrawString("Nessuna serie selezionata.", Font, Brushes.Gray, MarginLeft, 10);
            return;
        }

        var plotRect = PlotRect;
        var maxTotal = _data.Max(d => visible.Sum(s => Math.Max(0m, s.Selector(d))));
        if (maxTotal <= 0m)
        {
            maxTotal = 1m;
        }
        maxTotal *= 1.08m;

        using var gridPen = new Pen(Color.FromArgb(232, 232, 232));
        using var axisPen = new Pen(Color.FromArgb(160, 160, 160));
        using var labelBrush = new SolidBrush(Color.DimGray);

        const int yTicks = 5;
        for (var i = 0; i <= yTicks; i++)
        {
            var v = maxTotal * i / yTicks;
            var y = plotRect.Bottom - (float)(v / maxTotal) * plotRect.Height;
            g.DrawLine(gridPen, plotRect.Left, y, plotRect.Right, y);
            var label = FormatCompact(v);
            var size = g.MeasureString(label, _labelFont);
            g.DrawString(label, _labelFont, labelBrush, plotRect.Left - size.Width - 6, y - size.Height / 2);
        }

        var n = _data.Count;
        var slot = (float)plotRect.Width / n;
        var barWidth = Math.Max(2f, Math.Min(60f, slot * 0.6f));

        for (var i = 0; i < n; i++)
        {
            var x = plotRect.Left + i * slot + (slot - barWidth) / 2;
            var yCursor = (float)plotRect.Bottom;
            foreach (var s in visible)
            {
                var v = Math.Max(0m, s.Selector(_data[i]));
                var h = (float)(v / maxTotal) * plotRect.Height;
                using var brush = new SolidBrush(s.Color);
                g.FillRectangle(brush, x, yCursor - h, barWidth, h);
                yCursor -= h;
            }
        }

        var labelStep = Math.Max(1, (int)Math.Ceiling(n / 16.0));
        for (var i = 0; i < n; i += labelStep)
        {
            var x = plotRect.Left + i * slot + slot / 2;
            var label = _data[i].Date.ToString("MM/yy", ItCulture);
            var size = g.MeasureString(label, _labelFont);
            g.DrawString(label, _labelFont, labelBrush, x - size.Width / 2, plotRect.Bottom + 4);
        }

        g.DrawRectangle(axisPen, plotRect);

        if (_hoverIndex >= 0 && _hoverIndex < n)
        {
            var x = plotRect.Left + _hoverIndex * slot;
            using var hoverBrush = new SolidBrush(Color.FromArgb(28, Color.Black));
            g.FillRectangle(hoverBrush, x, plotRect.Top, slot, plotRect.Height);

            var snapshot = _data[_hoverIndex];
            var lines = new List<string> { snapshot.Date.ToString("MMMM yyyy", ItCulture) };
            lines.AddRange(visible.Select(s => $"{s.Name}: {s.Selector(snapshot).ToString("C0", ItCulture)}"));
            lines.Add($"Totale: {visible.Sum(s => s.Selector(snapshot)).ToString("C0", ItCulture)}");
            DrawTooltip(g, x + slot / 2, plotRect.Top, lines);
        }
    }

    private void DrawTooltip(Graphics g, float x, float top, List<string> lines)
    {
        var sizes = lines.Select(l => g.MeasureString(l, _labelFont)).ToList();
        var w = sizes.Max(s => s.Width) + 12;
        var h = sizes.Sum(s => s.Height) + 8;
        var boxX = x + 10;
        if (boxX + w > Width)
        {
            boxX = x - w - 10;
        }
        var boxY = top + 8;

        using var bg = new SolidBrush(Color.FromArgb(235, Color.White));
        using var border = new Pen(Color.Gray);
        g.FillRectangle(bg, boxX, boxY, w, h);
        g.DrawRectangle(border, boxX, boxY, w, h);

        var ty = boxY + 4;
        foreach (var line in lines)
        {
            g.DrawString(line, _labelFont, Brushes.Black, boxX + 6, ty);
            ty += g.MeasureString(line, _labelFont).Height;
        }
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (_data.Count == 0)
        {
            return;
        }

        var plotRect = PlotRect;
        if (e.X < plotRect.Left || e.X > plotRect.Right)
        {
            SetHover(-1);
            return;
        }

        var slot = (float)plotRect.Width / _data.Count;
        var idx = Math.Clamp((int)((e.X - plotRect.Left) / slot), 0, _data.Count - 1);
        SetHover(idx);
    }

    private void SetHover(int index)
    {
        if (index == _hoverIndex)
        {
            return;
        }
        _hoverIndex = index;
        Invalidate();
    }

    private static string FormatCompact(decimal v) => v.ToString("N0", ItCulture) + " €";
}

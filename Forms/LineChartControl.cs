using System.Drawing.Drawing2D;
using System.Globalization;
using DPOper.Services;

namespace DPOper.Forms;

/// <summary>
/// Grafico a linee disegnato "a mano" (senza dipendenze esterne) per confrontare più serie
/// giornaliere sullo stesso asse. Mostra griglia, legenda cliccabile-esterna (tramite
/// <see cref="SetSeriesVisible"/>) e un crosshair con tooltip al passaggio del mouse.
/// </summary>
public sealed class LineChartControl : Control
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

    public LineChartControl()
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

        if (_data.Count < 2)
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

        var maxV = visible.SelectMany(s => _data.Select(s.Selector)).DefaultIfEmpty(0m).Max();
        var minV = Math.Min(0m, visible.SelectMany(s => _data.Select(s.Selector)).DefaultIfEmpty(0m).Min());
        if (maxV <= minV)
        {
            maxV = minV + 1m;
        }
        maxV += (maxV - minV) * 0.05m;

        using var gridPen = new Pen(Color.FromArgb(232, 232, 232));
        using var axisPen = new Pen(Color.FromArgb(160, 160, 160));
        using var labelBrush = new SolidBrush(Color.DimGray);

        const int yTicks = 5;
        for (var i = 0; i <= yTicks; i++)
        {
            var v = minV + (maxV - minV) * i / yTicks;
            var y = ValueToY(v, minV, maxV, plotRect);
            g.DrawLine(gridPen, plotRect.Left, y, plotRect.Right, y);
            var label = FormatCompact(v);
            var size = g.MeasureString(label, _labelFont);
            g.DrawString(label, _labelFont, labelBrush, plotRect.Left - size.Width - 6, y - size.Height / 2);
        }

        var xTicks = Math.Min(7, _data.Count);
        for (var i = 0; i < xTicks; i++)
        {
            var idx = xTicks == 1 ? 0 : (int)Math.Round((_data.Count - 1) * (double)i / (xTicks - 1));
            var x = IndexToX(idx, plotRect);
            var label = _data[idx].Date.ToString("MM/yy", ItCulture);
            var size = g.MeasureString(label, _labelFont);
            g.DrawString(label, _labelFont, labelBrush, x - size.Width / 2, plotRect.Bottom + 4);
        }

        g.DrawRectangle(axisPen, plotRect);

        foreach (var s in visible)
        {
            using var pen = new Pen(s.Color, 2f) { LineJoin = LineJoin.Round };
            PointF? prev = null;
            for (var i = 0; i < _data.Count; i++)
            {
                var pt = new PointF(IndexToX(i, plotRect), ValueToY(s.Selector(_data[i]), minV, maxV, plotRect));
                if (prev is { } p)
                {
                    g.DrawLine(pen, p, pt);
                }
                prev = pt;
            }
        }

        if (_hoverIndex >= 0 && _hoverIndex < _data.Count)
        {
            var x = IndexToX(_hoverIndex, plotRect);
            using var crosshairPen = new Pen(Color.Gray) { DashStyle = DashStyle.Dash };
            g.DrawLine(crosshairPen, x, plotRect.Top, x, plotRect.Bottom);

            var snapshot = _data[_hoverIndex];
            var lines = new List<string> { snapshot.Date.ToString("dd/MM/yyyy", ItCulture) };
            lines.AddRange(visible.Select(s => $"{s.Name}: {s.Selector(snapshot).ToString("C0", ItCulture)}"));
            DrawTooltip(g, x, plotRect.Top, lines);
        }
    }

    private float IndexToX(int index, Rectangle plotRect) =>
        plotRect.Left + (float)index / Math.Max(1, _data.Count - 1) * plotRect.Width;

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

    private static float ValueToY(decimal value, decimal minV, decimal maxV, Rectangle plotRect)
    {
        var ratio = (float)((value - minV) / (maxV - minV));
        return plotRect.Bottom - ratio * plotRect.Height;
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (_data.Count < 2)
        {
            return;
        }

        var plotRect = PlotRect;
        if (e.X < plotRect.Left || e.X > plotRect.Right)
        {
            SetHover(-1);
            return;
        }

        var ratio = (double)(e.X - plotRect.Left) / plotRect.Width;
        var idx = Math.Clamp((int)Math.Round(ratio * (_data.Count - 1)), 0, _data.Count - 1);
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

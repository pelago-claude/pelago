using System.Drawing.Drawing2D;
using System.Globalization;
using DPOper.Services;

namespace DPOper.Forms;

/// <summary>
/// Colonne impilate pensate apposta per "quanto sono sopra/sotto i versamenti": ogni colonna
/// mostra il Totale, diviso in un segmento base fino ai Versamenti netti e un "cappello" colorato
/// (verde sopra se il Totale supera i versamenti, rosso se sta sotto) che è lo Scostamento. Una
/// linea tratteggiata collega i Versamenti netti di ogni colonna, come riferimento.
/// </summary>
public sealed class GapColumnChartControl : Control
{
    private const int MarginLeft = 95;
    private const int MarginRight = 24;
    private const int MarginTop = 16;
    private const int MarginBottom = 32;

    private static readonly CultureInfo ItCulture = CultureInfo.GetCultureInfo("it-IT");
    private readonly Font _labelFont = new("Segoe UI", 8f);

    private static readonly Color BaseColor = Color.SteelBlue;
    private static readonly Color GainColor = Color.MediumSeaGreen;
    private static readonly Color LossColor = Color.Crimson;
    private static readonly Color ReferenceLineColor = Color.FromArgb(90, 90, 90);

    private List<DailySnapshot> _data = new();
    private int _hoverIndex = -1;

    public GapColumnChartControl()
    {
        DoubleBuffered = true;
        BackColor = Color.White;
        MouseMove += OnMouseMove;
        MouseLeave += (_, _) => SetHover(-1);
    }

    public void SetData(List<DailySnapshot> data)
    {
        _data = data.OrderBy(d => d.Date).ToList();
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

        var plotRect = PlotRect;

        var maxV = _data.Max(d => Math.Max(d.Total, d.Contributions));
        var minV = Math.Min(0m, _data.Min(d => Math.Min(d.Total, d.Contributions)));
        if (maxV <= minV)
        {
            maxV = minV + 1m;
        }
        var pad = (maxV - minV) * 0.08m;
        maxV += pad;
        minV -= (minV < 0m ? pad : 0m);

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

        var n = _data.Count;
        var slot = (float)plotRect.Width / n;
        var barWidth = Math.Max(2f, Math.Min(60f, slot * 0.55f));

        for (var i = 0; i < n; i++)
        {
            var d = _data[i];
            var x = plotRect.Left + i * slot + (slot - barWidth) / 2;

            var lower = Math.Min(d.Contributions, d.Total);
            var upper = Math.Max(d.Contributions, d.Total);
            var yZero = ValueToY(0m, minV, maxV, plotRect);
            var yLower = ValueToY(lower, minV, maxV, plotRect);
            var yUpper = ValueToY(upper, minV, maxV, plotRect);

            using (var baseBrush = new SolidBrush(BaseColor))
            {
                g.FillRectangle(baseBrush, x, yLower, barWidth, yZero - yLower);
            }

            // Il "cappello" va sempre dal più piccolo dei due (versamenti/totale) al più grande,
            // colorato in base al segno dello scostamento — non dipende da quale dei due sia il totale.
            var capColor = d.Gap >= 0m ? GainColor : LossColor;
            using (var capBrush = new SolidBrush(capColor))
            {
                var capHeight = yLower - yUpper;
                if (capHeight > 0.5f)
                {
                    g.FillRectangle(capBrush, x, yUpper, barWidth, capHeight);
                }
            }
        }

        // Linea tratteggiata dei versamenti netti, collegando i centri delle colonne.
        using (var refPen = new Pen(ReferenceLineColor, 1.6f) { DashStyle = DashStyle.Dash })
        {
            PointF? prev = null;
            for (var i = 0; i < n; i++)
            {
                var x = plotRect.Left + i * slot + slot / 2;
                var y = ValueToY(_data[i].Contributions, minV, maxV, plotRect);
                var pt = new PointF(x, y);
                if (prev is { } p)
                {
                    g.DrawLine(refPen, p, pt);
                }
                prev = pt;
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

            var d = _data[_hoverIndex];
            var lines = new List<string>
            {
                d.Date.ToString("MMMM yyyy", ItCulture),
                $"Versamenti netti: {d.Contributions.ToString("C0", ItCulture)}",
                $"Totale: {d.Total.ToString("C0", ItCulture)}",
                $"Scostamento: {d.Gap.ToString("C0", ItCulture)}",
            };
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

    private static float ValueToY(decimal value, decimal minV, decimal maxV, Rectangle plotRect)
    {
        var ratio = (float)((value - minV) / (maxV - minV));
        return plotRect.Bottom - ratio * plotRect.Height;
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

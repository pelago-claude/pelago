using System.Drawing.Drawing2D;
using System.Globalization;
using DPOper.Services;

namespace DPOper.Forms;

/// <summary>
/// Colonne impilate per categoria (Disponibilità, Azioni, Fondi, Obbligazioni, Crypto, Derivati),
/// altezza = Totale, con una linea tratteggiata per i Versamenti netti e una tinta trasparente
/// verde (sopra) o rossa (sotto) che segnala lo scostamento senza coprire i colori di composizione.
/// Pensato per essere ospitato in un pannello scorrevole quando i punti sono molti (vista
/// giornaliera/settimanale): disegna in base alla propria Width, che il contenitore imposta.
/// </summary>
public sealed class CompositionColumnChartControl : Control
{
    private const int MarginLeft = 95;
    private const int MarginRight = 24;
    private const int MarginTop = 16;
    private const int MarginBottom = 32;

    private static readonly CultureInfo ItCulture = CultureInfo.GetCultureInfo("it-IT");
    private readonly Font _labelFont = new("Segoe UI", 8f);

    private static readonly string[] StackOrder = { "CASH", "STOCK", "FUND", "BOND", "CRYPTO", "DERIVATIVE" };

    private List<PortfolioAsOf> _data = new();
    private int _hoverIndex = -1;

    public CompositionColumnChartControl()
    {
        DoubleBuffered = true;
        BackColor = Color.White;
        MouseMove += OnMouseMove;
        MouseLeave += (_, _) => SetHover(-1);
    }

    public void SetData(List<PortfolioAsOf> data)
    {
        _data = data.OrderBy(d => d.Date).ToList();
        Invalidate();
    }

    private Rectangle PlotRect => new(MarginLeft, MarginTop,
        Math.Max(1, Width - MarginLeft - MarginRight), Math.Max(1, Height - MarginTop - MarginBottom));

    private static decimal ValueFor(PortfolioAsOf p, string key) =>
        key == "CASH" ? p.Cash : p.Assets.Where(a => a.AssetClass == key).Sum(a => a.Value);

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
        var barWidth = Math.Max(1f, Math.Min(60f, slot * 0.7f));
        var yZero = ValueToY(0m, minV, maxV, plotRect);

        for (var i = 0; i < n; i++)
        {
            var d = _data[i];
            var x = plotRect.Left + i * slot + (slot - barWidth) / 2;

            var yCursor = yZero;
            foreach (var key in StackOrder)
            {
                var v = ValueFor(d, key);
                if (v <= 0m)
                {
                    continue;
                }
                var segmentHeight = (float)(v / (maxV - minV)) * plotRect.Height;
                using var brush = new SolidBrush(AssetClassPalette.ColorFor(key));
                g.FillRectangle(brush, x, yCursor - segmentHeight, barWidth, segmentHeight);
                yCursor -= segmentHeight;
            }

            var yTotal = ValueToY(d.Total, minV, maxV, plotRect);
            var yContrib = ValueToY(d.Contributions, minV, maxV, plotRect);
            var overlayTop = Math.Min(yTotal, yContrib);
            var overlayHeight = Math.Abs(yTotal - yContrib);
            if (overlayHeight > 0.5f)
            {
                var overlayColor = d.Gap >= 0m ? Color.FromArgb(90, Color.MediumSeaGreen) : Color.FromArgb(110, Color.Crimson);
                using var overlayBrush = new SolidBrush(overlayColor);
                g.FillRectangle(overlayBrush, x, overlayTop, barWidth, overlayHeight);
            }
        }

        // Linea tratteggiata dei versamenti netti, collegando i centri delle colonne.
        using (var refPen = new Pen(Color.FromArgb(90, 90, 90), 1.6f) { DashStyle = DashStyle.Dash })
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

        var labelStep = Math.Max(1, (int)Math.Ceiling(n / 30.0));
        for (var i = 0; i < n; i += labelStep)
        {
            var x = plotRect.Left + i * slot + slot / 2;
            var label = _data[i].Date.ToString("dd/MM/yy", ItCulture);
            var size = g.MeasureString(label, _labelFont);
            g.DrawString(label, _labelFont, labelBrush, x - size.Width / 2, plotRect.Bottom + 4);
        }

        g.DrawRectangle(axisPen, plotRect);

        if (_hoverIndex >= 0 && _hoverIndex < n)
        {
            var x = plotRect.Left + _hoverIndex * slot;
            using var hoverBrush = new SolidBrush(Color.FromArgb(28, Color.Black));
            g.FillRectangle(hoverBrush, x, plotRect.Top, Math.Max(1f, slot), plotRect.Height);

            var d = _data[_hoverIndex];
            var lines = new List<(string Text, Color Color)> { (d.Date.ToString("dddd dd/MM/yyyy", ItCulture), Color.Black) };
            foreach (var key in StackOrder)
            {
                var v = ValueFor(d, key);
                if (v > 0m)
                {
                    lines.Add(($"{AssetClassPalette.LabelFor(key)}: {v.ToString("C0", ItCulture)}", AssetClassPalette.ColorFor(key)));
                }
            }
            lines.Add(($"Totale: {d.Total.ToString("C0", ItCulture)}", Color.Black));
            lines.Add(($"Versamenti netti: {d.Contributions.ToString("C0", ItCulture)}", Color.Black));
            var gapPercent = d.Contributions != 0m ? d.Gap / d.Contributions * 100m : 0m;
            lines.Add(($"Scostamento: {d.Gap.ToString("C0", ItCulture)} ({gapPercent:+0.0;-0.0;0}%)", Color.Black));

            if (_hoverIndex > 0)
            {
                var prev = _data[_hoverIndex - 1];
                lines.Add(PercentChangeLine("Totale vs colonna prec.", prev.Total, d.Total));
                lines.Add(PercentChangeLine("Asset vs colonna prec. (no disp.)", prev.AssetsTotal, d.AssetsTotal));
            }

            DrawTooltip(g, x + slot / 2, plotRect.Top, lines);
        }
    }

    private static (string Text, Color Color) PercentChangeLine(string label, decimal previous, decimal current)
    {
        var percent = previous != 0m ? (current - previous) / previous * 100m : 0m;
        var color = percent >= 0m ? Color.DarkGreen : Color.Crimson;
        return ($"{label}: {percent:+0.0;-0.0;0}%", color);
    }

    private void DrawTooltip(Graphics g, float x, float top, List<(string Text, Color Color)> lines)
    {
        var sizes = lines.Select(l => g.MeasureString(l.Text, _labelFont)).ToList();
        var w = sizes.Max(s => s.Width) + 12;
        var h = sizes.Sum(s => s.Height) + 8;
        var boxX = x + 10;
        if (boxX + w > Width)
        {
            boxX = x - w - 10;
        }
        var boxY = top + 8;

        using var bg = new SolidBrush(Color.FromArgb(240, Color.White));
        using var border = new Pen(Color.Gray);
        g.FillRectangle(bg, boxX, boxY, w, h);
        g.DrawRectangle(border, boxX, boxY, w, h);

        var ty = boxY + 4;
        foreach (var (text, color) in lines)
        {
            using var textBrush = new SolidBrush(color);
            g.DrawString(text, _labelFont, textBrush, boxX + 6, ty);
            ty += g.MeasureString(text, _labelFont).Height;
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

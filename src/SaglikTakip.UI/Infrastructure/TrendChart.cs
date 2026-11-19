using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;
using SaglikTakip.Business.Insights;

namespace SaglikTakip.UI.Infrastructure;

/// <summary>
/// Line chart of a daily trend drawn with GDI+: measured values, their moving average and optional
/// lower and upper limits. The x axis follows the calendar, so days without measurements show as gaps
/// in spacing. Windows Forms on .NET has no built-in chart control, and this avoids a third-party one.
/// </summary>
internal sealed class TrendChart : Control
{
    private static readonly Color ValueColor = Color.SteelBlue;
    private static readonly Color AverageColor = Color.DarkOrange;
    private static readonly Color LimitColor = Color.Firebrick;

    private IReadOnlyList<TrendPoint> _points = [];
    private double? _lowerLimit;
    private double? _upperLimit;

    public TrendChart()
    {
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw,
            true);
        BackColor = Color.White;
    }

    /// <summary>Text shown when there is nothing to draw.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string EmptyText { get; set; } = "Seçilen aralıkta ölçüm yok.";

    /// <summary>Format of the values on the y axis.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string ValueFormat { get; set; } = "0.#";

    public IReadOnlyList<TrendPoint> Points => _points;

    public void SetData(IReadOnlyList<TrendPoint> points, double? lowerLimit = null, double? upperLimit = null)
    {
        ArgumentNullException.ThrowIfNull(points);

        _points = points;
        _lowerLimit = lowerLimit;
        _upperLimit = upperLimit;
        Invalidate();
    }

    public void Clear(string emptyText)
    {
        EmptyText = emptyText;
        SetData([]);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPaint(e);
        Render(e.Graphics, ClientRectangle);
    }

    /// <summary>Draws the chart into <paramref name="bounds"/>; also used to render without a window.</summary>
    internal void Render(Graphics graphics, Rectangle bounds)
    {
        ArgumentNullException.ThrowIfNull(graphics);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var background = new SolidBrush(BackColor);
        graphics.FillRectangle(background, bounds);

        var font = Font;
        if (_points.Count == 0)
        {
            TextRenderer.DrawText(graphics, EmptyText, font, bounds, SystemColors.GrayText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            return;
        }

        var plot = ChartScale.PlotArea(bounds, font.Height);
        if (plot.Width < 10 || plot.Height < 10)
        {
            return;
        }

        var values = _points.Select(p => p.Value)
            .Concat(_points.Where(p => p.MovingAverage.HasValue).Select(p => p.MovingAverage!.Value));
        if (_lowerLimit.HasValue)
        {
            values = values.Append(_lowerLimit.Value);
        }

        if (_upperLimit.HasValue)
        {
            values = values.Append(_upperLimit.Value);
        }

        var (min, max) = ChartScale.ValueRange(values);
        var first = _points[0].Date;
        var last = _points[^1].Date;

        DrawAxes(graphics, plot, min, max, first, last, font);
        DrawLimit(graphics, plot, _lowerLimit, min, max);
        DrawLimit(graphics, plot, _upperLimit, min, max);

        var valuePoints = _points
            .Select(p => new PointF(ChartScale.MapX(p.Date, first, last, plot), ChartScale.MapY(p.Value, min, max, plot)))
            .ToArray();
        using (var valuePen = new Pen(ValueColor, 2f))
        using (var marker = new SolidBrush(ValueColor))
        {
            if (valuePoints.Length > 1)
            {
                graphics.DrawLines(valuePen, valuePoints);
            }

            foreach (var point in valuePoints)
            {
                graphics.FillEllipse(marker, point.X - 3, point.Y - 3, 6, 6);
            }
        }

        var averagePoints = _points
            .Where(p => p.MovingAverage.HasValue)
            .Select(p => new PointF(ChartScale.MapX(p.Date, first, last, plot), ChartScale.MapY(p.MovingAverage!.Value, min, max, plot)))
            .ToArray();
        if (averagePoints.Length > 1)
        {
            using var averagePen = new Pen(AverageColor, 2f) { DashStyle = DashStyle.Dash };
            graphics.DrawLines(averagePen, averagePoints);
        }

        DrawLegend(graphics, bounds, font);
    }

    private void DrawAxes(Graphics graphics, RectangleF plot, double min, double max, DateTime first, DateTime last, Font font)
    {
        using var axisPen = new Pen(SystemColors.ControlDark);
        using var gridPen = new Pen(Color.Gainsboro) { DashStyle = DashStyle.Dot };
        graphics.DrawLine(axisPen, plot.Left, plot.Top, plot.Left, plot.Bottom);
        graphics.DrawLine(axisPen, plot.Left, plot.Bottom, plot.Right, plot.Bottom);

        const int Ticks = 4;
        for (var i = 0; i <= Ticks; i++)
        {
            var value = min + ((max - min) * i / Ticks);
            var y = ChartScale.MapY(value, min, max, plot);
            graphics.DrawLine(gridPen, plot.Left, y, plot.Right, y);
            var text = value.ToString(ValueFormat, CultureInfo.CurrentCulture);
            var size = TextRenderer.MeasureText(text, font);
            TextRenderer.DrawText(graphics, text, font, new Point((int)plot.Left - size.Width - 4, (int)y - (size.Height / 2)), ForeColor);
        }

        DateTime[] dates = first == last ? [first] : [first, first.AddDays((last - first).Days / 2), last];
        foreach (var date in dates.Distinct())
        {
            var x = ChartScale.MapX(date, first, last, plot);
            var text = date.ToString("d", CultureInfo.CurrentCulture);
            var size = TextRenderer.MeasureText(text, font);
            var left = Math.Clamp(x - (size.Width / 2f), 0, Math.Max(0, Width - size.Width));
            TextRenderer.DrawText(graphics, text, font, new Point((int)left, (int)plot.Bottom + 4), ForeColor);
        }
    }

    private static void DrawLimit(Graphics graphics, RectangleF plot, double? limit, double min, double max)
    {
        if (!limit.HasValue)
        {
            return;
        }

        using var pen = new Pen(LimitColor, 1.5f) { DashStyle = DashStyle.DashDot };
        var y = ChartScale.MapY(limit.Value, min, max, plot);
        graphics.DrawLine(pen, plot.Left, y, plot.Right, y);
    }

    private void DrawLegend(Graphics graphics, Rectangle bounds, Font font)
    {
        (string Text, Color Color)[] items = _lowerLimit.HasValue || _upperLimit.HasValue
            ? [("Ölçüm", ValueColor), ("Hareketli ortalama", AverageColor), ("Hedef sınırı", LimitColor)]
            : [("Ölçüm", ValueColor), ("Hareketli ortalama", AverageColor)];

        var x = bounds.Right - 8;
        for (var i = items.Length - 1; i >= 0; i--)
        {
            var size = TextRenderer.MeasureText(items[i].Text, font);
            x -= size.Width;
            TextRenderer.DrawText(graphics, items[i].Text, font, new Point(x, bounds.Top + 4), ForeColor);
            x -= 16;
            using var brush = new SolidBrush(items[i].Color);
            graphics.FillRectangle(brush, x, bounds.Top + 4 + (size.Height / 2) - 2, 12, 4);
            x -= 12;
        }
    }
}

/// <summary>Coordinate mapping of <see cref="TrendChart"/>; pure functions so the scaling can be tested.</summary>
internal static class ChartScale
{
    /// <summary>Plot rectangle inside <paramref name="bounds"/>, leaving room for labels and the legend.</summary>
    public static RectangleF PlotArea(Rectangle bounds, int fontHeight) =>
        RectangleF.FromLTRB(
            bounds.Left + 56,
            bounds.Top + fontHeight + 12,
            bounds.Right - 16,
            bounds.Bottom - fontHeight - 10);

    /// <summary>Value range with 5% headroom; a single value gets a range of ±1 around it.</summary>
    public static (double Min, double Max) ValueRange(IEnumerable<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var list = values.ToList();
        if (list.Count == 0)
        {
            return (0, 1);
        }

        var min = list.Min();
        var max = list.Max();
        if (max - min < 1e-9)
        {
            return (min - 1, max + 1);
        }

        var padding = (max - min) * 0.05;
        return (min - padding, max + padding);
    }

    /// <summary>X position of <paramref name="date"/>; a single day is drawn in the middle.</summary>
    public static float MapX(DateTime date, DateTime first, DateTime last, RectangleF plot)
    {
        var span = (last - first).TotalDays;
        if (span <= 0)
        {
            return plot.Left + (plot.Width / 2);
        }

        return plot.Left + (float)((date - first).TotalDays / span * plot.Width);
    }

    /// <summary>Y position of <paramref name="value"/>; larger values are drawn higher.</summary>
    public static float MapY(double value, double min, double max, RectangleF plot)
    {
        if (max - min <= 0)
        {
            return plot.Top + (plot.Height / 2);
        }

        return plot.Bottom - (float)((value - min) / (max - min) * plot.Height);
    }
}

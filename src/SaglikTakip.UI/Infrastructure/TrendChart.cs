using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;
using SaglikTakip.Business.Insights;
using SaglikTakip.UI.Theming;

namespace SaglikTakip.UI.Infrastructure;

/// <summary>
/// Line chart of a daily trend drawn with GDI+: measured values, their moving average and the goal
/// range as a band. The x axis follows the calendar, so days without measurements show as gaps in
/// spacing. Colors come from the theme: a blue/orange series pair that stays distinct for color vision
/// deficiencies, recessive grid lines, and measurements outside the goal range marked in the alert
/// color (and listed by name next to the chart, so color is never the only cue). Windows Forms on .NET
/// has no built-in chart control, and this avoids a third-party one.
/// </summary>
internal sealed class TrendChart : Control
{
    private IReadOnlyList<TrendPoint> _points = [];
    private double? _lowerLimit;
    private double? _upperLimit;

    public TrendChart()
    {
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw,
            true);
        BackColor = Theme.Current.Surface;
        ForeColor = Theme.Current.Text;
        Font = Theme.Fonts.Caption;
        AccessibleRole = AccessibleRole.Chart;
        AccessibleName = "Trend grafiği";
    }

    /// <summary>Text shown when there is nothing to draw.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string EmptyText { get; set; } = "Seçilen aralıkta ölçüm yok.";

    /// <summary>Format of the values on the y axis.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string ValueFormat { get; set; } = "0.#";

    /// <summary>Unit appended to the label of the latest value, for example "kg".</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Unit { get; set; } = string.Empty;

    /// <summary>A request is pending; the empty state shows a loading message instead of "no data".</summary>
    public bool IsLoading { get; private set; }

    public IReadOnlyList<TrendPoint> Points => _points;

    public void SetData(IReadOnlyList<TrendPoint> points, double? lowerLimit = null, double? upperLimit = null)
    {
        ArgumentNullException.ThrowIfNull(points);

        _points = points;
        _lowerLimit = lowerLimit;
        _upperLimit = upperLimit;
        IsLoading = false;
        Invalidate();
    }

    public void Clear(string emptyText)
    {
        EmptyText = emptyText;
        SetData([]);
    }

    public void ShowLoading(string text)
    {
        Clear(text);
        IsLoading = true;
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

        var p = Theme.Current;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var background = new SolidBrush(BackColor))
        {
            graphics.FillRectangle(background, bounds);
        }

        if (_points.Count == 0)
        {
            DrawEmptyState(graphics, bounds, p);
            return;
        }

        var values = _points.Select(point => point.Value)
            .Concat(_points.Where(point => point.MovingAverage.HasValue).Select(point => point.MovingAverage!.Value));
        if (_lowerLimit.HasValue)
        {
            values = values.Append(_lowerLimit.Value);
        }

        if (_upperLimit.HasValue)
        {
            values = values.Append(_upperLimit.Value);
        }

        var (low, high) = ChartScale.ValueRange(values);
        var ticks = ChartScale.NiceTicks(low, high, 5);
        var (min, max) = (ticks[0], ticks[^1]);
        var format = ticks.Count > 1 && ticks[1] - ticks[0] < 1 ? ValueFormat : "0";

        var font = Font;
        var labelWidth = ticks.Max(t => TextRenderer.MeasureText(graphics, t.ToString(format, CultureInfo.CurrentCulture), font).Width);
        var legendHeight = font.Height + Theme.Px(this, Spacing.L);
        var latestWidth = TextRenderer.MeasureText(graphics, LatestLabel(), Theme.Fonts.BodyStrong).Width;
        var plot = RectangleF.FromLTRB(
            bounds.Left + labelWidth + Theme.Px(this, Spacing.M),
            bounds.Top + legendHeight,
            bounds.Right - latestWidth - Theme.Px(this, Spacing.M),
            bounds.Bottom - font.Height - Theme.Px(this, Spacing.M));
        if (plot.Width < 10 || plot.Height < 10)
        {
            return;
        }

        var first = _points[0].Date;
        var last = _points[^1].Date;
        DrawGrid(graphics, plot, ticks, min, max, format, font, p);
        DrawGoal(graphics, plot, min, max, p);
        DrawDates(graphics, plot, first, last, font, p);
        DrawSeries(graphics, plot, min, max, first, last, p);
        DrawLegend(graphics, bounds, plot, font, p);
    }

    private void DrawEmptyState(Graphics graphics, Rectangle bounds, ThemePalette p)
    {
        var glyphFont = Theme.Fonts.Glyph(22F);
        var body = Theme.Fonts.Body;
        var glyphHeight = glyphFont?.Height ?? 0;
        var gap = Theme.Px(this, Spacing.S);
        var top = bounds.Top + ((bounds.Height - glyphHeight - gap - (body.Height * 2)) / 2);
        Painting.DrawGlyph(graphics, IsLoading ? Glyphs.Sync : Glyphs.Chart, glyphFont, new Rectangle(bounds.Left, top, bounds.Width, glyphHeight), p.TextMuted);
        var textBounds = new Rectangle(bounds.Left + Theme.Px(this, Spacing.L), top + glyphHeight + gap, Math.Max(0, bounds.Width - Theme.Px(this, 2 * Spacing.L)), body.Height * 2);
        TextRenderer.DrawText(
            graphics,
            EmptyText,
            body,
            textBounds,
            p.TextSecondary,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
    }

    private void DrawGrid(Graphics graphics, RectangleF plot, IReadOnlyList<double> ticks, double min, double max, string format, Font font, ThemePalette p)
    {
        using var gridPen = new Pen(p.ChartGrid, Math.Max(1F, Theme.PxF(this, 1)));
        using var axisPen = new Pen(p.ChartAxis, Math.Max(1F, Theme.PxF(this, 1)));
        foreach (var tick in ticks)
        {
            var y = ChartScale.MapY(tick, min, max, plot);
            graphics.DrawLine(tick <= min ? axisPen : gridPen, plot.Left, y, plot.Right, y);
            var text = tick.ToString(format, CultureInfo.CurrentCulture);
            var size = TextRenderer.MeasureText(graphics, text, font);
            TextRenderer.DrawText(
                graphics,
                text,
                font,
                new Point((int)plot.Left - size.Width - Theme.Px(this, Spacing.S), (int)y - (size.Height / 2)),
                p.TextMuted);
        }
    }

    private void DrawGoal(Graphics graphics, RectangleF plot, double min, double max, ThemePalette p)
    {
        if (_lowerLimit is { } lower && _upperLimit is { } upper)
        {
            var top = ChartScale.MapY(upper, min, max, plot);
            var bottom = ChartScale.MapY(lower, min, max, plot);
            using var band = new SolidBrush(p.ChartGoalBand);
            graphics.FillRectangle(band, plot.Left, top, plot.Width, bottom - top);
        }

        using var pen = new Pen(p.ChartGoal, Math.Max(1F, Theme.PxF(this, 1.25F))) { DashStyle = DashStyle.Dash };
        foreach (var limit in new[] { _lowerLimit, _upperLimit })
        {
            if (limit is { } value)
            {
                var y = ChartScale.MapY(value, min, max, plot);
                graphics.DrawLine(pen, plot.Left, y, plot.Right, y);
            }
        }
    }

    private void DrawDates(Graphics graphics, RectangleF plot, DateTime first, DateTime last, Font font, ThemePalette p)
    {
        var sample = TextRenderer.MeasureText(graphics, first.ToString("dd MMM", CultureInfo.CurrentCulture), font).Width;
        var maxLabels = Math.Max(2, (int)(plot.Width / (sample + Theme.Px(this, Spacing.XL))));
        using var tickPen = new Pen(p.ChartAxis, Math.Max(1F, Theme.PxF(this, 1)));
        foreach (var date in ChartScale.DateTicks(first, last, maxLabels))
        {
            var x = ChartScale.MapX(date, first, last, plot);
            graphics.DrawLine(tickPen, x, plot.Bottom, x, plot.Bottom + Theme.PxF(this, 4));
            var text = date.ToString("dd MMM", CultureInfo.CurrentCulture);
            var size = TextRenderer.MeasureText(graphics, text, font);
            var left = Math.Clamp(x - (size.Width / 2F), 0, Math.Max(0, Width - size.Width));
            TextRenderer.DrawText(graphics, text, font, new Point((int)left, (int)plot.Bottom + Theme.Px(this, 6)), p.TextMuted);
        }
    }

    private void DrawSeries(Graphics graphics, RectangleF plot, double min, double max, DateTime first, DateTime last, ThemePalette p)
    {
        var line = Theme.PxF(this, 2F);
        var averagePoints = _points
            .Where(point => point.MovingAverage.HasValue)
            .Select(point => new PointF(ChartScale.MapX(point.Date, first, last, plot), ChartScale.MapY(point.MovingAverage!.Value, min, max, plot)))
            .ToArray();
        if (averagePoints.Length > 1)
        {
            using var averagePen = new Pen(p.ChartAverage, line) { DashStyle = DashStyle.Dash, LineJoin = LineJoin.Round };
            graphics.DrawLines(averagePen, averagePoints);
        }

        var valuePoints = _points
            .Select(point => new PointF(ChartScale.MapX(point.Date, first, last, plot), ChartScale.MapY(point.Value, min, max, plot)))
            .ToArray();
        if (valuePoints.Length > 1)
        {
            using var valuePen = new Pen(p.ChartValue, line) { LineJoin = LineJoin.Round };
            graphics.DrawLines(valuePen, valuePoints);
        }

        var radius = Theme.PxF(this, 4F);
        var ring = Theme.PxF(this, 2F);
        using var ringBrush = new SolidBrush(BackColor);
        using var valueBrush = new SolidBrush(p.ChartValue);
        using var alertBrush = new SolidBrush(p.ChartAlert);
        for (var i = 0; i < valuePoints.Length; i++)
        {
            var point = valuePoints[i];
            var outside = IsOutside(_points[i].Value);
            var r = outside ? radius + Theme.PxF(this, 1F) : radius;
            graphics.FillEllipse(ringBrush, point.X - r - ring, point.Y - r - ring, 2 * (r + ring), 2 * (r + ring));
            graphics.FillEllipse(outside ? alertBrush : valueBrush, point.X - r, point.Y - r, 2 * r, 2 * r);
        }

        // The latest value is labelled directly at the end of the line; other values are read from the axis.
        var latest = valuePoints[^1];
        var text = LatestLabel();
        var font = Theme.Fonts.BodyStrong;
        var size = TextRenderer.MeasureText(graphics, text, font);
        var x = latest.X + Theme.Px(this, Spacing.S);
        var y = Math.Clamp(latest.Y - (size.Height / 2F), plot.Top - (size.Height / 2F), plot.Bottom - (size.Height / 2F));
        TextRenderer.DrawText(graphics, text, font, new Point((int)x, (int)y), p.Text);
    }

    private string LatestLabel() =>
        _points[^1].Value.ToString(ValueFormat, CultureInfo.CurrentCulture) + (Unit.Length > 0 ? " " + Unit : string.Empty);

    private bool IsOutside(double value) =>
        (_lowerLimit is { } lower && value < lower) || (_upperLimit is { } upper && value > upper);

    private void DrawLegend(Graphics graphics, Rectangle bounds, RectangleF plot, Font font, ThemePalette p)
    {
        var items = new List<(string Text, LegendMark Mark, Color Color)>
        {
            ("Ölçüm", LegendMark.Line, p.ChartValue),
            ("Hareketli ortalama", LegendMark.Dashed, p.ChartAverage),
        };
        if (_lowerLimit.HasValue && _upperLimit.HasValue)
        {
            items.Add(("Hedef aralığı", LegendMark.Band, p.ChartGoal));
        }
        else if (_lowerLimit.HasValue || _upperLimit.HasValue)
        {
            items.Add(("Hedef sınırı", LegendMark.Dashed, p.ChartGoal));
        }

        if (_points.Any(point => IsOutside(point.Value)))
        {
            items.Add(("Sınır dışı", LegendMark.Dot, p.ChartAlert));
        }

        var x = plot.Left;
        var y = bounds.Top + Theme.Px(this, 2);
        var swatch = Theme.PxF(this, 18);
        var center = y + (font.Height / 2F);
        foreach (var (text, mark, color) in items)
        {
            DrawLegendMark(graphics, mark, color, new RectangleF(x, center - Theme.PxF(this, 5), swatch, Theme.PxF(this, 10)), p);
            x += swatch + Theme.Px(this, 6);
            TextRenderer.DrawText(graphics, text, font, new Point((int)x, y), p.TextSecondary);
            x += TextRenderer.MeasureText(graphics, text, font).Width + Theme.Px(this, Spacing.L);
        }
    }

    private void DrawLegendMark(Graphics graphics, LegendMark mark, Color color, RectangleF area, ThemePalette p)
    {
        var middle = area.Top + (area.Height / 2F);
        switch (mark)
        {
            case LegendMark.Band:
            {
                using var band = new SolidBrush(p.ChartGoalBand);
                graphics.FillRectangle(band, area);
                using var edge = new Pen(color, Math.Max(1F, Theme.PxF(this, 1.25F))) { DashStyle = DashStyle.Dash };
                graphics.DrawLine(edge, area.Left, area.Top, area.Right, area.Top);
                graphics.DrawLine(edge, area.Left, area.Bottom, area.Right, area.Bottom);
                break;
            }

            case LegendMark.Dot:
            {
                using var brush = new SolidBrush(color);
                var r = Theme.PxF(this, 5F);
                graphics.FillEllipse(brush, area.Left + (area.Width / 2F) - r, middle - r, 2 * r, 2 * r);
                break;
            }

            default:
            {
                using var pen = new Pen(color, Theme.PxF(this, 2F));
                if (mark == LegendMark.Dashed)
                {
                    pen.DashStyle = DashStyle.Dash;
                }

                graphics.DrawLine(pen, area.Left, middle, area.Right, middle);
                if (mark == LegendMark.Line)
                {
                    using var brush = new SolidBrush(color);
                    var r = Theme.PxF(this, 4F);
                    graphics.FillEllipse(brush, area.Left + (area.Width / 2F) - r, middle - r, 2 * r, 2 * r);
                }

                break;
            }
        }
    }

    private enum LegendMark
    {
        Line,
        Dashed,
        Band,
        Dot,
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

    /// <summary>
    /// Axis ticks on "nice" steps (1, 2 or 5 times a power of ten) that cover <paramref name="min"/> to
    /// <paramref name="max"/> with at most about <paramref name="maxTicks"/> values.
    /// </summary>
    public static IReadOnlyList<double> NiceTicks(double min, double max, int maxTicks)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxTicks, 2);
        if (max - min < 1e-9)
        {
            (min, max) = (min - 1, max + 1);
        }

        var step = NiceNumber(NiceNumber(max - min, round: false) / (maxTicks - 1), round: true);
        var first = Math.Floor(min / step) * step;
        var last = Math.Ceiling(max / step) * step;
        var ticks = new List<double>();
        for (var value = first; value <= last + (step / 2); value += step)
        {
            ticks.Add(Math.Round(value, 10));
        }

        return ticks;
    }

    /// <summary>Up to <paramref name="maxLabels"/> evenly spaced whole days from <paramref name="first"/> to <paramref name="last"/>, both included.</summary>
    public static IReadOnlyList<DateTime> DateTicks(DateTime first, DateTime last, int maxLabels)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLabels, 2);

        var days = (last.Date - first.Date).Days;
        if (days <= 0)
        {
            return [first.Date];
        }

        var step = Math.Max(1, (int)Math.Ceiling(days / (double)(maxLabels - 1)));
        var ticks = new List<DateTime>();
        for (var day = 0; day < days; day += step)
        {
            ticks.Add(first.Date.AddDays(day));
        }

        // The last day is always labelled; drop the previous tick when it would sit too close.
        if (ticks.Count > 1 && days - ((ticks.Count - 1) * step) < step / 2.0)
        {
            ticks.RemoveAt(ticks.Count - 1);
        }

        ticks.Add(last.Date);
        return ticks;
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

    private static double NiceNumber(double range, bool round)
    {
        var exponent = Math.Floor(Math.Log10(range));
        var fraction = range / Math.Pow(10, exponent);
        double nice = round
            ? fraction < 1.5 ? 1 : fraction < 3 ? 2 : fraction < 7 ? 5 : 10
            : fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10;
        return nice * Math.Pow(10, exponent);
    }
}

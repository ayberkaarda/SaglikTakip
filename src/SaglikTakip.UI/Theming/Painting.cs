using System.Drawing.Drawing2D;

namespace SaglikTakip.UI.Theming;

/// <summary>Drawing helpers shared by the custom painted controls.</summary>
internal static class Painting
{
    /// <summary>A rounded rectangle path; a radius of 0 gives a plain rectangle.</summary>
    public static GraphicsPath RoundedRect(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0.5F)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var arc = new RectangleF(bounds.Location, new SizeF(diameter, diameter));
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>Fills and optionally outlines a rounded rectangle; the outline is drawn inside the bounds.</summary>
    public static void FillRounded(Graphics graphics, RectangleF bounds, float radius, Color fill, Color? border = null, float borderWidth = 1F)
    {
        ArgumentNullException.ThrowIfNull(graphics);

        var previous = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var inset = border.HasValue ? borderWidth / 2F : 0F;
        var shape = RectangleF.Inflate(bounds, -inset, -inset);
        using (var path = RoundedRect(shape, Math.Max(0, radius - inset)))
        {
            if (fill.A > 0)
            {
                using var brush = new SolidBrush(fill);
                graphics.FillPath(brush, path);
            }

            if (border is { A: > 0 } color)
            {
                using var pen = new Pen(color, borderWidth);
                graphics.DrawPath(pen, path);
            }
        }

        graphics.SmoothingMode = previous;
    }

    /// <summary>Draws a glyph from the icon font centered in <paramref name="bounds"/>; nothing when no icon font exists.</summary>
    public static void DrawGlyph(Graphics graphics, string glyph, Font? font, Rectangle bounds, Color color)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        if (font is null || string.IsNullOrEmpty(glyph))
        {
            return;
        }

        TextRenderer.DrawText(
            graphics,
            glyph,
            font,
            bounds,
            color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
    }

    /// <summary>Background color of the closest ancestor that paints one, for clearing rounded corners.</summary>
    public static Color ParentBackground(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        for (var parent = control.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent.BackColor.A == 255)
            {
                return parent.BackColor;
            }
        }

        return Theme.Current.Page;
    }
}

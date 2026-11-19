using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using SaglikTakip.UI.Theming;

namespace SaglikTakip.Screenshots;

/// <summary>
/// Draws a form that is never shown. Window handles are created without making anything visible,
/// the content is painted with <see cref="Control.DrawToBitmap"/> and a neutral window frame is
/// drawn around it, so the image does not depend on the desktop theme of the machine running the tool.
/// </summary>
internal static class OffscreenCapture
{
    public static Bitmap Capture(Form form, float scale, bool minimizeBox)
    {
        ArgumentNullException.ThrowIfNull(form);

        // The form's content moves to a hidden, parentless panel: Windows Forms limits a form to the size
        // of the screen, which the 2x images exceed, and the panel has no frame to cut away. The panel
        // is never visible, so its window stays hidden under the framework's parking window.
        var clientSize = form.ClientSize;
        using var host = new Panel
        {
            Visible = false,
            BackColor = form.BackColor,
            ForeColor = form.ForeColor,
            Font = form.Font,
            Size = clientSize,
        };
        host.Controls.AddRange(form.Controls.Cast<Control>().ToArray());

        CreateHandles(host);
        if (Math.Abs(scale - 1F) > 0.001F)
        {
            host.Scale(new SizeF(scale, scale));
        }

        host.Size = new Size((int)Math.Round(clientSize.Width * scale), (int)Math.Round(clientSize.Height * scale));
        // Auto-sized panels settle after their children: lay out top-down, bottom-up, then top-down again.
        LayoutAll(host);
        LayoutChildrenFirst(host);
        LayoutAll(host);
        Application.DoEvents();

        using var content = new Bitmap(host.Width, host.Height, PixelFormat.Format32bppArgb);
        host.DrawToBitmap(content, new Rectangle(Point.Empty, host.Size));
        PaintComboBoxes(host, content);
        return Frame(content, new Rectangle(Point.Empty, host.Size), form.Text, scale, minimizeBox);
    }

    public static void Save(Bitmap bitmap, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        bitmap.Save(path, ImageFormat.Png);
    }

    /// <summary>Handles of hidden child controls are not created on their own; WM_PRINT skips controls without one.</summary>
    private static void CreateHandles(Control control)
    {
        _ = control.Handle;
        foreach (Control child in control.Controls)
        {
            CreateHandles(child);
        }
    }

    /// <summary>Drop-down lists answer WM_PRINT natively; their themed drawing is added on top.</summary>
    private static void PaintComboBoxes(Control root, Bitmap target)
    {
        using var graphics = Graphics.FromImage(target);
        foreach (var combo in Descendants(root).OfType<ThemedComboBox>())
        {
            if (combo.Width <= 0 || combo.Height <= 0)
            {
                continue;
            }

            var location = Point.Empty;
            for (Control? current = combo; current is not null && current != root; current = current.Parent)
            {
                location.Offset(current.Left, current.Top);
            }

            using var image = new Bitmap(combo.Width, combo.Height, PixelFormat.Format24bppRgb);
            using (var comboGraphics = Graphics.FromImage(image))
            {
                combo.PaintTo(comboGraphics);
            }

            graphics.DrawImageUnscaled(image, location);
        }
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (Control child in control.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child))
            {
                yield return nested;
            }
        }
    }

    private static void LayoutChildrenFirst(Control control)
    {
        foreach (Control child in control.Controls)
        {
            LayoutChildrenFirst(child);
        }

        control.PerformLayout();
    }

    private static void LayoutAll(Control control)
    {
        control.PerformLayout();
        foreach (Control child in control.Controls)
        {
            LayoutAll(child);
        }
    }

    /// <summary>A light title bar with icon, title and caption glyphs, a hairline border and rounded corners.</summary>
    private static Bitmap Frame(Bitmap window, Rectangle client, string title, float scale, bool minimizeBox)
    {
        var p = Theme.Current;
        int S(float value) => (int)Math.Round(value * scale);
        var titleHeight = S(32);
        var border = Math.Max(1, S(1));
        var width = client.Width + (2 * border);
        var height = client.Height + titleHeight + border;

        var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(result);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var shape = Painting.RoundedRect(new RectangleF(0, 0, width - 1, height - 1), S(8));
        graphics.SetClip(shape);
        // GDI text needs an opaque target, so the title bar is drawn on its own bitmap first.
        using (var bar = TitleBar(width, titleHeight + border, title, scale, minimizeBox))
        {
            graphics.DrawImage(bar, 0, 0);
        }

        graphics.DrawImage(window, new Rectangle(border, titleHeight, client.Width, client.Height), client, GraphicsUnit.Pixel);
        graphics.ResetClip();
        using var pen = new Pen(Color.FromArgb(70, 0, 0, 0), border);
        graphics.DrawPath(pen, shape);
        return result;
    }

    private static Bitmap TitleBar(int width, int height, string title, float scale, bool minimizeBox)
    {
        var p = Theme.Current;
        int S(float value) => (int)Math.Round(value * scale);
        var bar = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(bar);
        graphics.Clear(p.Surface);

        var iconSize = S(16);
        using (var icon = new Icon(Theme.AppIcon, new Size(iconSize, iconSize)))
        {
            graphics.DrawIcon(icon, new Rectangle(S(12), (height - iconSize) / 2, iconSize, iconSize));
        }

        using var titleFont = new Font("Segoe UI", 9F * scale, FontStyle.Regular, GraphicsUnit.Point);
        TextRenderer.DrawText(graphics, title, titleFont, new Rectangle(S(38), 0, width - S(200), height), p.Text, p.Surface,
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        // Minimize (when the window has one), maximize and close glyphs, right aligned.
        string[] glyphs = minimizeBox ? ["\uE921", "\uE922", "\uE8BB"] : ["\uE922", "\uE8BB"];
        using var glyphFont = Theme.GlyphFamily is { } family ? new Font(family, 7.5F * scale, FontStyle.Regular, GraphicsUnit.Point) : null;
        var buttonWidth = S(46);
        for (var i = 0; i < glyphs.Length; i++)
        {
            var x = width - (buttonWidth * (glyphs.Length - i));
            Painting.DrawGlyph(graphics, glyphs[i], glyphFont, new Rectangle(x, 0, buttonWidth, height), p.Text);
        }

        return bar;
    }
}

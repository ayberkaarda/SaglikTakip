using System.ComponentModel;

namespace SaglikTakip.UI.Theming;

/// <summary>Visual weight of a button: one primary action per area, the rest secondary.</summary>
internal enum ButtonKind
{
    Primary,
    Secondary,
    Danger,
}

/// <summary>
/// A standard <see cref="Button"/> (keyboard, mnemonics, accessibility and AcceptButton behavior are
/// unchanged) painted with the theme: rounded corners, hover and pressed states and a visible focus
/// ring. Under high contrast the native button is used.
/// </summary>
internal sealed class ThemedButton : Button
{
    private bool _hover;
    private bool _pressed;

    public ThemedButton(ButtonKind kind = ButtonKind.Secondary)
    {
        Kind = kind;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimumSize = new Size(Sizes.ButtonMinWidth, Sizes.ControlHeight);
        Padding = new Padding(Spacing.M, 0, Spacing.M, 0);
        Margin = new Padding(Spacing.XS);
        Font = Theme.Fonts.BodyStrong;
        Cursor = Cursors.Hand;

        if (Theme.Current.IsHighContrast)
        {
            FlatStyle = FlatStyle.System;
            return;
        }

        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor,
            true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ButtonKind Kind { get; set; }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        ArgumentNullException.ThrowIfNull(mevent);
        if (mevent.Button == MouseButtons.Left)
        {
            _pressed = true;
            Invalidate();
        }

        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(mevent);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        _hover = false;
        _pressed = false;
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        ArgumentNullException.ThrowIfNull(pevent);
        if (Theme.Current.IsHighContrast)
        {
            base.OnPaint(pevent);
            return;
        }

        var p = Theme.Current;
        var g = pevent.Graphics;
        g.Clear(Painting.ParentBackground(this));

        var (fill, border, text) = Colors(p);
        var radius = Theme.PxF(this, Radius.Control);
        var ring = Theme.PxF(this, Sizes.FocusRing);
        var bounds = new RectangleF(0, 0, Width, Height);
        Painting.FillRounded(g, bounds, radius, fill, border);

        if (Focused && ShowFocusCues)
        {
            // Primary: a ring in the text color inside the fill; others: an accent ring on the edge.
            var primary = Kind == ButtonKind.Primary;
            var focus = primary ? RectangleF.Inflate(bounds, -ring, -ring) : bounds;
            Painting.FillRounded(g, focus, radius, Color.Transparent, primary ? p.OnAccent : p.Accent, ring);
        }

        TextRenderer.DrawText(
            g,
            Text,
            Font,
            ClientRectangle,
            text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
                | TextFormatFlags.EndEllipsis | (ShowKeyboardCues ? 0 : TextFormatFlags.HidePrefix));
    }

    private (Color Fill, Color? Border, Color Text) Colors(ThemePalette p)
    {
        if (!Enabled)
        {
            return (p.SurfaceAlt, p.Border, p.TextDisabled);
        }

        return Kind switch
        {
            ButtonKind.Primary => (_pressed ? p.AccentPressed : _hover ? p.AccentHover : p.Accent, null, p.OnAccent),
            ButtonKind.Danger => (_pressed ? p.DangerSubtle : _hover ? p.DangerSubtle : p.Surface, p.Danger, p.Danger),
            _ => (_pressed ? p.Border : _hover ? p.SurfaceHover : p.Surface, p.BorderStrong, p.Text),
        };
    }
}

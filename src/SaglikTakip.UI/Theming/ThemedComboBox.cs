using System.ComponentModel;

namespace SaglikTakip.UI.Theming;

/// <summary>
/// A drop-down list painted with the theme (the closed box and the list items), with a placeholder
/// shown while nothing is selected. Selection, keyboard and accessibility stay native. Under high
/// contrast the native look is kept.
/// </summary>
internal sealed class ThemedComboBox : ComboBox
{
    private bool _hover;

    public ThemedComboBox()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        Font = Theme.Fonts.Body;
        Margin = new Padding(0, Spacing.XS, 0, Spacing.XS);
        if (Theme.Current.IsHighContrast)
        {
            return;
        }

        DrawMode = DrawMode.OwnerDrawFixed;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        // ItemHeight is left at its default (the font height plus a small gap), which matches the text
        // boxes next to it. Setting it would create the window handle in the constructor.
    }

    /// <summary>Text shown in muted color while no item is selected, for example "Kullanıcı seçin".</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Placeholder { get; set; } = string.Empty;

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        Invalidate();
        base.OnSelectedIndexChanged(e);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (Theme.Current.IsHighContrast || e.Index < 0)
        {
            base.OnDrawItem(e);
            return;
        }

        var p = Theme.Current;
        var selected = (e.State & DrawItemState.Selected) != 0;
        using (var brush = new SolidBrush(selected ? p.AccentSubtle : p.Surface))
        {
            e.Graphics.FillRectangle(brush, e.Bounds);
        }

        var text = GetItemText(Items[e.Index]);
        var bounds = Rectangle.Inflate(e.Bounds, -Theme.Px(this, Spacing.S), 0);
        TextRenderer.DrawText(e.Graphics, text, Font, bounds, p.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (Theme.Current.IsHighContrast)
        {
            base.OnPaint(e);
            return;
        }

        var p = Theme.Current;
        var g = e.Graphics;
        g.Clear(Painting.ParentBackground(this));

        var focused = Focused || DroppedDown;
        var border = !Enabled ? p.Border : focused ? p.Accent : _hover ? p.TextSecondary : p.BorderStrong;
        var width = focused ? Theme.PxF(this, Sizes.FocusRing) : Math.Max(1F, Theme.PxF(this, 1));
        Painting.FillRounded(g, new RectangleF(0, 0, Width, Height), Theme.PxF(this, Radius.Control), Enabled ? p.Surface : p.SurfaceAlt, border, width);

        var chevronWidth = Theme.Px(this, 28);
        var textBounds = new Rectangle(Theme.Px(this, Spacing.S + 2), 0, Math.Max(0, Width - chevronWidth - Theme.Px(this, Spacing.S)), Height);
        var hasSelection = SelectedIndex >= 0;
        var text = hasSelection ? GetItemText(SelectedItem) : Placeholder;
        var color = !Enabled ? p.TextDisabled : hasSelection ? p.Text : p.TextMuted;
        TextRenderer.DrawText(g, text, Font, textBounds, color, Enabled ? p.Surface : p.SurfaceAlt, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

        var chevron = new Rectangle(Width - chevronWidth, 0, chevronWidth - Theme.Px(this, 4), Height);
        Painting.DrawGlyph(g, Glyphs.ChevronDown, Theme.Fonts.Glyph(8F), chevron, Enabled ? p.TextSecondary : p.TextDisabled);
    }

    /// <summary>
    /// Paints the closed box into <paramref name="graphics"/>. The native control answers WM_PRINT
    /// itself, so DrawToBitmap (used by the offscreen screenshot tool) needs this entry point.
    /// </summary>
    internal void PaintTo(Graphics graphics) => OnPaint(new PaintEventArgs(graphics, ClientRectangle));
}

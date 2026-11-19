using System.ComponentModel;

namespace SaglikTakip.UI.Theming;

/// <summary>
/// A main menu entry: glyph, title and a one line description on a card. It is a real
/// <see cref="Button"/>, so Tab, Enter, Space and screen readers work as usual.
/// </summary>
internal sealed class TileButton : Button
{
    private bool _hover;
    private bool _pressed;

    public TileButton(string name, string title, string description, string glyph)
    {
        Name = name;
        Text = title;
        Description = description;
        Glyph = glyph;
        AccessibleName = title;
        AccessibleDescription = description;
        Dock = DockStyle.Fill;
        Margin = new Padding(0, 0, Spacing.M, Spacing.M);
        Font = Theme.Fonts.Subtitle;
        Cursor = Cursors.Hand;
        UseMnemonic = false;
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
            true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Description { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Glyph { get; set; }

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
        _pressed = true;
        Invalidate();
        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(mevent);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        ArgumentNullException.ThrowIfNull(pevent);

        var p = Theme.Current;
        var g = pevent.Graphics;
        g.Clear(Painting.ParentBackground(this));

        var focused = Focused && ShowFocusCues;
        var hairline = Math.Max(1F, Theme.PxF(this, 1));
        var fill = _pressed ? p.SurfaceAlt : _hover ? p.SurfaceHover : p.Surface;
        var border = focused ? p.Accent : _hover ? p.BorderStrong : p.Border;
        Painting.FillRounded(g, new RectangleF(0, 0, Width, Height), Theme.PxF(this, Radius.Card), fill, border, focused ? Theme.PxF(this, Sizes.FocusRing) : hairline);

        var pad = Theme.Px(this, Spacing.L);
        var badge = Theme.Px(this, Sizes.TileBadge);
        var badgeBounds = new Rectangle(pad, pad, badge, badge);
        Painting.FillRounded(g, badgeBounds, Theme.PxF(this, Radius.Card), p.IsHighContrast ? p.Surface : p.AccentSubtle, p.IsHighContrast ? p.Border : null);
        Painting.DrawGlyph(g, Glyph, Theme.Fonts.Glyph(14F), badgeBounds, p.AccentText);

        var left = badgeBounds.Right + Theme.Px(this, Spacing.M);
        var width = Math.Max(0, Width - left - pad);
        var titleHeight = Font.Height;
        TextRenderer.DrawText(
            g,
            Text,
            Font,
            new Rectangle(left, pad - Theme.Px(this, 2), width, titleHeight),
            Enabled ? p.Text : p.TextDisabled,
            TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        var caption = Theme.Fonts.Caption;
        var descriptionTop = pad + titleHeight;
        TextRenderer.DrawText(
            g,
            Description,
            caption,
            new Rectangle(left, descriptionTop, width, Math.Max(0, Height - descriptionTop - Theme.Px(this, Spacing.S))),
            p.TextSecondary,
            TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl);
    }
}

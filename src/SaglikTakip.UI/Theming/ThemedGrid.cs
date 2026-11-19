using System.ComponentModel;

namespace SaglikTakip.UI.Theming;

/// <summary>
/// Read-only list grid with the theme's table style: quiet header, horizontal row dividers, row
/// selection and a message in place of the rows when the list is empty.
/// </summary>
internal sealed class ThemedGrid : DataGridView
{
    public ThemedGrid()
    {
        ReadOnly = true;
        AllowUserToAddRows = false;
        AllowUserToDeleteRows = false;
        AllowUserToResizeRows = false;
        SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        MultiSelect = false;
        RowHeadersVisible = false;
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        StandardTab = true; // Tab leaves the grid instead of walking its cells.
        DoubleBuffered = true;
        ThemeStyler.StyleGrid(this);
    }

    /// <summary>Shown in the middle of the grid while it has no rows.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string EmptyText { get; set; } = string.Empty;

    protected override void OnPaint(PaintEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPaint(e);
        if (Rows.Count > 0 || string.IsNullOrEmpty(EmptyText))
        {
            return;
        }

        var top = ColumnHeadersVisible && ColumnCount > 0 ? ColumnHeadersHeight : 0;
        var area = new Rectangle(Theme.Px(this, Spacing.L), top, Math.Max(0, Width - Theme.Px(this, 2 * Spacing.L)), Math.Max(0, Height - top));
        TextRenderer.DrawText(
            e.Graphics,
            EmptyText,
            Theme.Fonts.Body,
            area,
            Theme.Current.TextMuted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
    }

    protected override void OnDataBindingComplete(DataGridViewBindingCompleteEventArgs e)
    {
        base.OnDataBindingComplete(e);
        Invalidate();
    }
}

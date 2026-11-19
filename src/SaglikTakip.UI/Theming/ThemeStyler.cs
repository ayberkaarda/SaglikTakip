namespace SaglikTakip.UI.Theming;

/// <summary>
/// Applies the current palette to standard Windows Forms controls, so forms do not set colors one by
/// one. Custom painted controls read the palette themselves. Under high contrast nothing is recolored
/// and the system colors stay in effect.
/// </summary>
internal static class ThemeStyler
{
    /// <summary>Styles <paramref name="root"/> and all of its children.</summary>
    public static void Apply(Control root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var p = Theme.Current;
        if (p.IsHighContrast)
        {
            return;
        }

        if (root is Form form)
        {
            form.BackColor = p.Page;
            form.ForeColor = p.Text;
        }

        Walk(root, p);
    }

    /// <summary>Table style for a <see cref="DataGridView"/>; padding follows the control's DPI.</summary>
    public static void StyleGrid(DataGridView grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        var p = Theme.Current;
        grid.Font = Theme.Fonts.Body;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        var cellPadding = new Padding(Theme.Px(grid, Spacing.S), Theme.Px(grid, 5), Theme.Px(grid, Spacing.S), Theme.Px(grid, 5));
        grid.ColumnHeadersDefaultCellStyle.Padding = cellPadding;
        grid.ColumnHeadersDefaultCellStyle.Font = Theme.Fonts.BodyStrong;
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
        grid.DefaultCellStyle.Padding = cellPadding;
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;

        if (p.IsHighContrast)
        {
            return;
        }

        grid.EnableHeadersVisualStyles = false;
        grid.BackgroundColor = p.Surface;
        grid.GridColor = p.Border;
        grid.ColumnHeadersDefaultCellStyle.BackColor = p.SurfaceAlt;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = p.TextSecondary;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = p.SurfaceAlt;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = p.TextSecondary;
        grid.DefaultCellStyle.BackColor = p.Surface;
        grid.DefaultCellStyle.ForeColor = p.Text;
        grid.DefaultCellStyle.SelectionBackColor = p.AccentSubtle;
        grid.DefaultCellStyle.SelectionForeColor = p.Text;
        grid.AlternatingRowsDefaultCellStyle.BackColor = p.SurfaceAlt;
    }

    private static void Walk(Control parent, ThemePalette p)
    {
        foreach (Control control in parent.Controls)
        {
            switch (control)
            {
                case TextBox box:
                    // A borderless box (for example a read-only preview) blends into its card.
                    if (box.BorderStyle == BorderStyle.Fixed3D)
                    {
                        box.BorderStyle = BorderStyle.FixedSingle;
                    }

                    box.BackColor = box.BorderStyle == BorderStyle.None ? control.Parent?.BackColor ?? p.Surface : p.Surface;
                    box.ForeColor = p.Text;
                    break;
                case NumericUpDown number:
                    number.BorderStyle = BorderStyle.FixedSingle;
                    number.BackColor = p.Surface;
                    number.ForeColor = p.Text;
                    break;
                case DateTimePicker picker:
                    picker.CalendarMonthBackground = p.Surface;
                    picker.CalendarForeColor = p.Text;
                    picker.CalendarTitleBackColor = p.Accent;
                    picker.CalendarTitleForeColor = p.OnAccent;
                    break;
                case ListBox list:
                    list.BorderStyle = BorderStyle.None;
                    list.BackColor = p.Surface;
                    list.ForeColor = p.Text;
                    break;
                case CheckBox check:
                    check.ForeColor = p.Text;
                    break;
            }

            if (control.HasChildren && control is not DataGridView and not NumericUpDown and not DateTimePicker)
            {
                Walk(control, p);
            }
        }
    }
}

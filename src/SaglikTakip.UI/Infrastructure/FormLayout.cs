using SaglikTakip.UI.Theming;

namespace SaglikTakip.UI.Infrastructure;

/// <summary>
/// Factories for the building blocks of the screens: header, cards, labelled fields, inputs and
/// buttons. Everything uses the theme tokens and logical pixels, and lays out with table and flow
/// panels, so no screen positions or colors controls by hand.
/// </summary>
internal static class FormLayout
{
    /// <summary>Header of a screen: glyph badge, title and one line of description.</summary>
    public static Control Header(string title, string subtitle, string glyph, Image? logo = null)
    {
        var p = Theme.Current;
        var header = new TableLayoutPanel
        {
            Name = "header",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(Spacing.XL, Spacing.L, Spacing.XL, Spacing.L),
            BackColor = p.Surface,
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.Paint += (_, e) => DrawDivider(header, e.Graphics, top: false);

        var badge = new GlyphBadge(glyph, Sizes.HeaderBadge) { Name = "headerBadge", Image = logo, Anchor = AnchorStyles.Left };
        var titleLabel = new Label
        {
            Name = "headerTitle",
            Text = title,
            AutoSize = true,
            Font = Theme.Fonts.Title,
            ForeColor = p.Text,
            UseMnemonic = false,
            Margin = new Padding(0),
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
        };
        var subtitleLabel = new Label
        {
            Name = "headerSubtitle",
            Text = subtitle,
            AutoSize = true,
            Font = Theme.Fonts.Body,
            ForeColor = p.TextSecondary,
            UseMnemonic = false,
            Margin = new Padding(0, Spacing.XS / 2, 0, 0),
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
        };
        header.Controls.Add(badge, 0, 0);
        header.SetRowSpan(badge, 2);
        header.Controls.Add(titleLabel, 1, 0);
        header.Controls.Add(subtitleLabel, 1, 1);
        return header;
    }

    /// <summary>A card with an optional section title above <paramref name="content"/>.</summary>
    public static CardPanel Card(string name, string? title, Control content, bool fill = true)
    {
        ArgumentNullException.ThrowIfNull(content);

        var card = new CardPanel { Name = name, Dock = fill ? DockStyle.Fill : DockStyle.Top };
        content.Dock = fill ? DockStyle.Fill : DockStyle.Top;
        card.Controls.Add(content);
        if (title is not null)
        {
            var heading = SectionTitle(title);
            heading.Dock = DockStyle.Top;
            heading.Padding = new Padding(0, 0, 0, Spacing.M);
            card.Controls.Add(heading);
        }

        if (!fill)
        {
            card.AutoSize = true;
            card.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        }

        return card;
    }

    public static Label SectionTitle(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = Theme.Fonts.Subtitle,
        ForeColor = Theme.Current.Text,
        UseMnemonic = false,
        Margin = new Padding(0, 0, 0, Spacing.S),
    };

    /// <summary>Label of an input; an ampersand marks the Alt access key that moves to the input.</summary>
    public static Label Caption(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = Theme.Fonts.BodyStrong,
        ForeColor = Theme.Current.Text,
        Margin = new Padding(0, 0, 0, Spacing.XS),
    };

    /// <summary>Secondary explanatory text.</summary>
    public static Label Hint(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = Theme.Fonts.Caption,
        ForeColor = Theme.Current.TextMuted,
        UseMnemonic = false,
        Margin = new Padding(0, Spacing.XS, 0, 0),
    };

    /// <summary>A read-only value, for example the latest weight.</summary>
    public static Label Value(string name) => new()
    {
        Name = name,
        AutoSize = true,
        Font = Theme.Fonts.Value,
        ForeColor = Theme.Current.Text,
        UseMnemonic = false,
        Margin = new Padding(0, 0, Spacing.M, 0),
    };

    /// <summary>Body text that wraps inside its column.</summary>
    public static Label Text(string name) => new()
    {
        Name = name,
        AutoSize = true,
        Font = Theme.Fonts.Body,
        ForeColor = Theme.Current.Text,
        UseMnemonic = false,
        Margin = new Padding(0),
    };

    public static ThemedButton Button(string name, string text, ButtonKind kind = ButtonKind.Secondary) =>
        new(kind) { Name = name, Text = text };

    public static ThemedComboBox UserPicker(string name = "userPicker") => new()
    {
        Name = name,
        Width = 280,
        Placeholder = "Kullanıcı seçin",
        AccessibleName = "Kullanıcı",
    };

    public static ThemedComboBox Picker(string name, int width) => new() { Name = name, Width = width };

    public static DateTimePicker DatePicker(string name, DateTime value) =>
        new() { Name = name, Format = DateTimePickerFormat.Short, Width = 130, Value = value, Margin = new Padding(0, Spacing.XS, 0, Spacing.XS) };

    /// <summary>
    /// A labelled input: caption above, input below and an optional hint. The caption's access key
    /// moves the focus to the input, and the input gets the caption as its accessible name.
    /// </summary>
    public static TableLayoutPanel Field(string caption, Control input, string? hint = null)
    {
        ArgumentNullException.ThrowIfNull(input);

        var field = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, Spacing.L, Spacing.M),
        };
        field.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        field.Controls.Add(Caption(caption));
        if (input.Dock == DockStyle.None)
        {
            input.Anchor = input is ThemedComboBox or TextBox
                ? AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
                : AnchorStyles.Left | AnchorStyles.Top;
        }

        input.Margin = new Padding(0, Spacing.XS, 0, 0);
        input.AccessibleName ??= caption.Replace("&", string.Empty, StringComparison.Ordinal);
        field.Controls.Add(input);
        if (hint is not null)
        {
            field.Controls.Add(Hint(hint));
        }

        return field;
    }

    /// <summary>
    /// Fields in rows; every row is split evenly across the widest row, and a single field in a row
    /// spans the full width.
    /// </summary>
    public static TableLayoutPanel Fields(string name, params Control[][] rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var columns = Math.Max(1, rows.Max(r => r.Length));
        var grid = new TableLayoutPanel
        {
            Name = name,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = columns,
            RowCount = rows.Length,
            Dock = DockStyle.Top,
            Margin = Padding.Empty,
        };
        for (var c = 0; c < columns; c++)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / columns));
        }

        for (var r = 0; r < rows.Length; r++)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var row = rows[r];
            for (var c = 0; c < row.Length; c++)
            {
                grid.Controls.Add(row[c], c, r);
                if (row.Length == 1)
                {
                    grid.SetColumnSpan(row[c], columns);
                }
            }
        }

        return grid;
    }

    /// <summary>
    /// Controls stacked top to bottom in a single auto-sized column; unlike docked panels it grows
    /// when a child grows (for example when a label wraps on a narrow window or at a high DPI).
    /// </summary>
    public static TableLayoutPanel Stack(string name, params Control[] controls)
    {
        ArgumentNullException.ThrowIfNull(controls);

        var stack = new TableLayoutPanel
        {
            Name = name,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = controls.Length,
            Dock = DockStyle.Top,
            Margin = Padding.Empty,
        };
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var control in controls)
        {
            stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            control.Dock = DockStyle.Fill;
            stack.Controls.Add(control);
        }

        return stack;
    }

    /// <summary>
    /// The page layout of a screen: rows top to bottom that take their content's height, except the
    /// rows marked to fill, which share the remaining height.
    /// </summary>
    public static TableLayoutPanel Page(params (Control Control, bool Fill)[] rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var page = new TableLayoutPanel
        {
            Name = "page",
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = rows.Length,
            Margin = Padding.Empty,
        };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var (control, fill) in rows)
        {
            page.RowStyles.Add(fill ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.AutoSize));
            control.Dock = DockStyle.Fill;
            page.Controls.Add(control);
        }

        // Without a filling row the table would stretch the last row; an empty row takes the rest instead.
        if (!rows.Any(row => row.Fill))
        {
            page.RowCount++;
            page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        }

        return page;
    }

    /// <summary>A left-to-right row that wraps on narrow windows.</summary>
    public static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        row.Controls.AddRange(controls);
        return row;
    }

    /// <summary>A hairline at the top or bottom edge of <paramref name="control"/>.</summary>
    public static void DrawDivider(Control control, Graphics graphics, bool top)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(graphics);

        using var pen = new Pen(Theme.Current.Border, Math.Max(1, Theme.Px(control, 1)));
        var y = top ? 0 : control.Height - 1;
        graphics.DrawLine(pen, 0, y, control.Width, y);
    }
}

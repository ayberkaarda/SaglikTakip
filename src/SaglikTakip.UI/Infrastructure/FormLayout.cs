namespace SaglikTakip.UI.Infrastructure;

/// <summary>
/// Small helpers for the forms whose layout is built in code (trends, goals, reminders, period report).
/// They use standard window frames and table layouts so the controls stay aligned at any DPI.
/// </summary>
internal static class FormLayout
{
    public static void ApplyDefaults(Form form, string title, Size clientSize)
    {
        ArgumentNullException.ThrowIfNull(form);

        form.Text = title;
        form.Font = new Font("Segoe UI", 9F);
        form.AutoScaleMode = AutoScaleMode.Font;
        form.ClientSize = clientSize;
        form.MinimumSize = new Size(clientSize.Width / 2, clientSize.Height / 2);
        form.StartPosition = FormStartPosition.CenterParent;
        form.ShowInTaskbar = false;
        form.MinimizeBox = false;
    }

    public static Label Caption(string text) =>
        new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 3, 3) };

    public static Label Value(string name) =>
        new() { Name = name, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 12, 3) };

    public static Button Button(string name, string text) =>
        new() { Name = name, Text = text, AutoSize = true, MinimumSize = new Size(90, 28), Margin = new Padding(3) };

    public static ComboBox UserPicker(string name = "userPicker") =>
        new() { Name = name, DropDownStyle = ComboBoxStyle.DropDownList, Width = 240, Anchor = AnchorStyles.Left };

    public static DateTimePicker DatePicker(string name, DateTime value) =>
        new() { Name = name, Format = DateTimePickerFormat.Short, Width = 110, Value = value, Anchor = AnchorStyles.Left };

    /// <summary>A left-to-right row that wraps on narrow windows.</summary>
    public static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Padding = new Padding(6, 4, 6, 4),
        };
        row.Controls.AddRange(controls);
        return row;
    }

    /// <summary>A two-column label/input grid.</summary>
    public static TableLayoutPanel Grid(params (string Caption, Control Input)[] rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Padding = new Padding(6),
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var (caption, input) in rows)
        {
            grid.Controls.Add(Caption(caption));
            grid.Controls.Add(input);
        }

        return grid;
    }
}

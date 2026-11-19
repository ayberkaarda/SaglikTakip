using SaglikTakip.UI.Theming;

namespace SaglikTakip.UI.Infrastructure;

/// <summary>Something that can show an inline result or validation message.</summary>
internal interface IStatusSurface
{
    void ShowStatus(StatusKind kind, string message);
}

/// <summary>
/// Common frame of every screen opened from the main menu: a header with glyph, title and a short
/// description; an inline message strip; the content area; and a footer with the actions on the
/// right (primary first, Kapat last). Layout uses docking and table layouts in logical pixels and is
/// scaled per monitor by Windows Forms (<see cref="AutoScaleMode.Dpi"/>).
/// </summary>
public abstract class AppDialog : Form, IStatusSurface
{
    internal const string AppName = "Sağlık Takip";

    private readonly InfoBar _status = new();
    private readonly FlowLayoutPanel _actions;
    private readonly Label _footerNote;

    private protected AppDialog(string title, string subtitle, string glyph, Size clientSize)
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = Theme.Fonts.Body;
        Text = AppName + " | " + title;
        Icon = Theme.AppIcon;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        ClientSize = clientSize;
        MinimumSize = SizeFromClientSize(new Size(clientSize.Width * 3 / 4, clientSize.Height * 3 / 4));

        Content = new Panel { Name = "content", Dock = DockStyle.Fill, Margin = Padding.Empty };
        var body = new Panel
        {
            Name = "body",
            Dock = DockStyle.Fill,
            Padding = new Padding(Spacing.XL, Spacing.L, Spacing.XL, Spacing.S),
        };
        body.Controls.Add(Content);
        body.Controls.Add(_status);

        (_actions, _footerNote, var footer) = BuildFooter();
        Controls.Add(body);
        Controls.Add(footer);
        Controls.Add(FormLayout.Header(title, subtitle, glyph));
    }

    /// <summary>The area between the message strip and the footer; screens add their layout here.</summary>
    private protected Panel Content { get; }

    /// <summary>The inline message strip.</summary>
    internal InfoBar StatusBar => _status;

    /// <summary>Shows a result or validation message inside the window; it never takes the focus.</summary>
    internal void ShowStatus(StatusKind kind, string message) => _status.Show(kind, message);

    internal void ClearStatus() => _status.Clear();

    void IStatusSurface.ShowStatus(StatusKind kind, string message) => ShowStatus(kind, message);

    /// <summary>A short hint on the left of the footer.</summary>
    private protected void SetFooterNote(string text) => _footerNote.Text = text;

    /// <summary>Places the footer buttons left to right; Tab follows the same order.</summary>
    private protected void SetFooterButtons(params Button[] buttons)
    {
        ArgumentNullException.ThrowIfNull(buttons);

        // The panel flows right to left, so the last button is added first.
        for (var i = buttons.Length - 1; i >= 0; i--)
        {
            _actions.Controls.Add(buttons[i]);
        }

        for (var i = 0; i < buttons.Length; i++)
        {
            buttons[i].TabIndex = i;
        }
    }

    /// <summary>A "Kapat" button that closes the window; also bound to Esc.</summary>
    private protected Button CreateCloseButton(string name = "btnKapat")
    {
        var close = FormLayout.Button(name, "Kapat", ButtonKind.Secondary);
        close.Click += (_, _) => Close();
        CancelButton = close;
        return close;
    }

    /// <summary>Applies the theme to everything that was added and resumes layout; call at the end of the constructor.</summary>
    private protected void FinishLayout()
    {
        ThemeStyler.Apply(this);
        ResumeLayout(false);
        PerformLayout();
    }

    private bool _sizeFitted;

    /// <inheritdoc />
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        // Measured once the window exists, so the real fonts and metrics decide how fields wrap.
        if (!_sizeFitted)
        {
            _sizeFitted = true;
            FitSizeToContent();
        }
    }

    /// <summary>
    /// Makes the window large enough for its content: the content has no scrolling, so neither the
    /// initial size nor the minimum size may cut an input off, also while a two-line message strip is
    /// shown. The layout is tried at the narrowest width with such a strip and grown until nothing
    /// overflows; the minimum stays at three quarters of the design size when that is already enough.
    /// </summary>
    private void FitSizeToContent()
    {
        var design = ClientSize;
        var trial = new Size(MinimumSize.Width - (Width - ClientSize.Width), MinimumSize.Height - (Height - ClientSize.Height));
        ShowStatus(StatusKind.Info, "Örnek ileti" + Environment.NewLine + "ikinci satır");
        try
        {
            for (var pass = 0; pass < 4; pass++)
            {
                ClientSize = trial;
                PerformLayout();
                var overflow = Overflow(this);
                if (overflow.IsEmpty)
                {
                    break;
                }

                trial += overflow;
            }
        }
        finally
        {
            ClearStatus();
        }

        var fitted = SizeFromClientSize(trial);
        MinimumSize = new Size(Math.Max(MinimumSize.Width, fitted.Width), Math.Max(MinimumSize.Height, fitted.Height));
        ClientSize = new Size(Math.Max(design.Width, trial.Width), Math.Max(design.Height, trial.Height));
        PerformLayout();
    }

    /// <summary>How far the controls reach beyond the visible area of their containers (right, bottom).</summary>
    private static Size Overflow(Control root)
    {
        var width = 0;
        var height = 0;
        foreach (Control child in root.Controls)
        {
            if (child is InfoBar { Visible: false } || child.Width == 0 || child.Height == 0)
            {
                continue;
            }

            if (child is not (Label or Panel or TableLayoutPanel or FlowLayoutPanel or GroupBox) && child.Parent is not NumericUpDown)
            {
                // Position of the control in the client area of each ancestor, without creating handles.
                var offset = child.Location;
                for (var ancestor = child.Parent; ancestor is not null; ancestor = ancestor.Parent)
                {
                    width = Math.Max(width, offset.X + child.Width - ancestor.ClientSize.Width);
                    height = Math.Max(height, offset.Y + child.Height - ancestor.ClientSize.Height);
                    offset = new Point(offset.X + ancestor.Left, offset.Y + ancestor.Top);
                }
            }

            var nested = Overflow(child);
            width = Math.Max(width, nested.Width);
            height = Math.Max(height, nested.Height);
        }

        return new Size(width, height);
    }

    private static (FlowLayoutPanel Actions, Label Note, Control Footer) BuildFooter()
    {
        var footer = new TableLayoutPanel
        {
            Name = "footer",
            Dock = DockStyle.Bottom,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(Spacing.XL - Spacing.XS, Spacing.M, Spacing.XL - Spacing.XS, Spacing.M),
            BackColor = Theme.Current.Surface,
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.Paint += (_, e) => FormLayout.DrawDivider(footer, e.Graphics, top: true);

        var note = FormLayout.Hint(string.Empty);
        note.Name = "footerNote";
        note.AutoSize = false;
        note.AutoEllipsis = true;
        note.Dock = DockStyle.Fill;
        note.TextAlign = ContentAlignment.MiddleLeft;
        note.Margin = new Padding(Spacing.XS, 0, Spacing.M, 0);
        var actions = new FlowLayoutPanel
        {
            Name = "actions",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Anchor = AnchorStyles.Right,
            Margin = Padding.Empty,
        };
        footer.Controls.Add(note, 0, 0);
        footer.Controls.Add(actions, 1, 0);
        return (actions, note, footer);
    }
}

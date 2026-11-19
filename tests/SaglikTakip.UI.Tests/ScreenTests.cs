using SaglikTakip.Domain.Entities;
using SaglikTakip.UI.Infrastructure;
using SaglikTakip.UI.Tests.Support;
using SaglikTakip.UI.Theming;
using static SaglikTakip.UI.Tests.Support.StaThread;

namespace SaglikTakip.UI.Tests;

/// <summary>Every screen is built with test doubles and laid out without being shown.</summary>
public sealed class ScreenTests
{
    private static readonly string[] LastStops = ["btnKapat", "btnkapat", "cikis"];

    public static TheoryData<string> ScreenNames =>
    [
        nameof(Form1), nameof(KullaniciEkleForm), nameof(SaglikKayitForm), nameof(EgzersizKayitForm), nameof(Raporekleform),
        nameof(TrendForm), nameof(HedefForm), nameof(HatirlaticiForm), nameof(DonemRaporuForm),
    ];

    [Theory]
    [MemberData(nameof(ScreenNames))]
    public void Screen_HasNeutralIdentity(string name) => Run(() =>
    {
        using var form = Create(name);

        Assert.StartsWith("Sağlık Takip", form.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Gov", form.Text, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(form.Icon);
        Assert.Equal(FormBorderStyle.Sizable, form.FormBorderStyle);
        Assert.True(form.MinimumSize.Width > 0 && form.MinimumSize.Height > 0, "A resizable screen needs a minimum size.");
        Assert.Equal(AutoScaleMode.Dpi, form.AutoScaleMode);
        Assert.Null(form.BackgroundImage);
    });

    [Theory]
    [MemberData(nameof(ScreenNames))]
    public void Screen_LaysOutInsideItsWindow(string name) => Run(() =>
    {
        using var form = Create(name);
        CreateHandles(form);
        form.PerformLayout();

        var overflow = Descendants(form)
            .Where(control => control.Parent is Form or Panel or TableLayoutPanel or FlowLayoutPanel)
            .Where(control => control is not InfoBar && !IsInsideParent(control)) // The message strip is hidden until used.
            .Select(control => $"{control.Name} ({control.GetType().Name}) {control.Bounds} in {control.Parent!.Name} {control.Parent.ClientRectangle}")
            .ToList();

        Assert.Empty(overflow);
    });

    [Theory]
    [MemberData(nameof(ScreenNames))]
    public void Screen_UsesThemedButtonsAndAKeyboardWayOut(string name) => Run(() =>
    {
        using var form = Create(name);

        var buttons = Descendants(form).OfType<Button>().ToList();
        Assert.NotEmpty(buttons);
        Assert.All(buttons, button => Assert.True(button is ThemedButton or TileButton, $"{button.Name} is not themed."));
        Assert.All(buttons, button => Assert.False(string.IsNullOrWhiteSpace(button.Text), $"{button.Name} has no text."));
        if (form is AppDialog)
        {
            Assert.NotNull(form.CancelButton); // Esc closes every screen.
        }
    });

    [Theory]
    [InlineData(nameof(KullaniciEkleForm), "textBoxad")]
    [InlineData(nameof(SaglikKayitForm), "comboBox1")]
    [InlineData(nameof(EgzersizKayitForm), "comboBox1")]
    [InlineData(nameof(TrendForm), "userPicker")]
    [InlineData(nameof(HedefForm), "userPicker")]
    [InlineData(nameof(HatirlaticiForm), "userPicker")]
    [InlineData(nameof(DonemRaporuForm), "userPicker")]
    [InlineData(nameof(Form1), "btnKullaniciEkle")]
    public void TabOrder_StartsAtTheFirstInputAndEndsInTheFooter(string name, string first) => Run(() =>
    {
        using var form = Create(name);
        CreateHandles(form);

        var order = TabOrder(form);

        Assert.Equal(first, order[0].Name);
        Assert.Contains(order[^1].Name, LastStops);
    });

    [Fact]
    public void InvalidAge_IsReportedInlineAndFocusesTheField() => Run(() =>
    {
        var users = new PendingUserService();
        using var form = new KullaniciEkleForm(users);
        Find<TextBox>(form, "textBoxad").Text = "Ayşe";
        var dialogs = NoDialogs.Requested.Count;

        var save = form.SaveAsync();

        Assert.True(save.IsCompletedSuccessfully);
        Assert.Equal(0, users.AddCalls);
        Assert.Equal(StatusKind.Warning, form.StatusBar.Kind);
        Assert.Equal("Lütfen geçerli bir yaş girin.", form.StatusBar.Text);
        Assert.Equal("Lütfen geçerli bir yaş girin.", form.StatusBar.AccessibleName);
        Assert.Equal(dialogs, NoDialogs.Requested.Count); // No message box.
    });

    [Fact]
    public void EmptyGrid_ExplainsWhatAppearsThere() => Run(() =>
    {
        using var form = new Raporekleform(new PendingUserService(), new ControlledReportService());

        var grid = Find<ThemedGrid>(form, "dataSaglikKaydi");

        Assert.Equal(0, grid.Rows.Count);
        Assert.Contains("Kullanıcı seçtiğinizde", grid.EmptyText, StringComparison.Ordinal);
    });

    private static Form Create(string name) => name switch
    {
        nameof(Form1) => new Form1(new UnusedFormFactory(), new ControlledReminderService()),
        nameof(KullaniciEkleForm) => new KullaniciEkleForm(new PendingUserService()),
        nameof(SaglikKayitForm) => new SaglikKayitForm(new PendingUserService(), new PendingHealthRecordService(), new ControlledGoalService { AnswerImmediately = true }),
        nameof(EgzersizKayitForm) => new EgzersizKayitForm(new PendingUserService(), new PendingExerciseService()),
        nameof(Raporekleform) => new Raporekleform(new PendingUserService(), new ControlledReportService()),
        nameof(TrendForm) => new TrendForm(new PendingUserService(), new ControlledTrendService(), new ControlledGoalService { AnswerImmediately = true }),
        nameof(HedefForm) => new HedefForm(new PendingUserService(), new ControlledGoalService { AnswerImmediately = true }),
        nameof(HatirlaticiForm) => new HatirlaticiForm(new PendingUserService(), new ControlledReminderService()),
        nameof(DonemRaporuForm) => new DonemRaporuForm(new PendingUserService(), new ControlledPeriodReportService()),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    /// <summary>Creates the window handles of a form that is never shown, so layout and tab order are real.</summary>
    private static void CreateHandles(Control control)
    {
        _ = control.Handle;
        foreach (Control child in control.Controls)
        {
            CreateHandles(child);
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

    private static bool IsInsideParent(Control control)
    {
        const int Tolerance = 1;
        var area = control.Parent!.ClientRectangle;
        area.Inflate(Tolerance, Tolerance);
        return control.Width == 0 || control.Height == 0 || area.Contains(control.Bounds); // An empty label has no size.
    }

    /// <summary>Focusable controls in the order Tab visits them.</summary>
    private static List<Control> TabOrder(Form form)
    {
        var order = new List<Control>();
        Control? current = null;
        while ((current = form.GetNextControl(current, forward: true)) is not null)
        {
            if (current.CanSelectWhenHidden())
            {
                order.Add(current);
            }
        }

        return order;
    }

    private sealed class UnusedFormFactory : IFormFactory
    {
        public T Create<T>()
            where T : Form => throw new InvalidOperationException("No screen is opened in these tests.");
    }
}

internal static class SelectionExtensions
{
    /// <summary>
    /// Whether Tab can stop on the control once the window is shown: a tab stop that is enabled and
    /// not a container, label or read-only presentation surface.
    /// </summary>
    public static bool CanSelectWhenHidden(this Control control) =>
        control.TabStop
        && control.Enabled
        && control is not (Label or Panel or TableLayoutPanel or FlowLayoutPanel or GroupBox or UserControl)
        && !control.GetType().Name.Contains("UpDownEdit", StringComparison.Ordinal)
        && !control.GetType().Name.Contains("UpDownButtons", StringComparison.Ordinal)
        && control.Parent is not NumericUpDown;
}

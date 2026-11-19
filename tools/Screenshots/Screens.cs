using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using SaglikTakip.Business.Services;
using SaglikTakip.UI;
using SaglikTakip.UI.Infrastructure;

namespace SaglikTakip.Screenshots;

/// <summary>The screens shown in the README, each prepared with synthetic data as a user would see it.</summary>
internal static class Screens
{
    /// <summary>File name (kebab-case, without extension) and how to build the screen.</summary>
    public static IReadOnlyList<(string Name, bool MinimizeBox, Func<IServiceProvider, Form> Build)> All { get; } =
    [
        ("ana-sayfa", true, Home),
        ("kullanici-ekle", false, AddUser),
        ("saglik-kaydi", false, HealthRecord),
        ("egzersiz-planla", false, Exercise),
        ("rapor", false, Report),
        ("trend-grafigi", false, Trend),
        ("hedefler", false, Goals),
        ("hatirlaticilar", false, Reminders),
        ("donem-raporu", false, PeriodReport),
    ];

    private static Form1 Home(IServiceProvider services) =>
        new Form1(new UnusedFormFactory(), services.GetRequiredService<IReminderService>());

    private static KullaniciEkleForm AddUser(IServiceProvider services)
    {
        var form = ActivatorUtilities.CreateInstance<KullaniciEkleForm>(services);
        Find<TextBox>(form, "textBoxad").Text = "Elif Demir";
        Find<TextBox>(form, "textBoxyas").Text = "34";
        Find<ComboBox>(form, "comboBoxcins").SelectedIndex = 1;
        return form;
    }

    private static SaglikKayitForm HealthRecord(IServiceProvider services)
    {
        var form = ActivatorUtilities.CreateInstance<SaglikKayitForm>(services);
        SelectFirstUser(form, "comboBox1", services);
        Find<NumericUpDown>(form, "numericUpDown1").Value = 77;
        Find<NumericUpDown>(form, "numericUpDown2").Value = 74;
        Find<TextBox>(form, "textBox1").Text = "Akşam yemeğinden sonra ölçüldü.";

        // Saving with a weight above the goal range shows the inline warning instead of a dialog.
        Run(form.SaveAsync());
        return form;
    }

    private static EgzersizKayitForm Exercise(IServiceProvider services)
    {
        var form = ActivatorUtilities.CreateInstance<EgzersizKayitForm>(services);
        SelectFirstUser(form, "comboBox1", services);
        Find<TextBox>(form, "textBox1").Text = "Tempolu yürüyüş";
        Find<NumericUpDown>(form, "numericUpDown1").Value = 40;
        Find<NumericUpDown>(form, "numericUpDown2").Value = 1;
        return form;
    }

    private static Raporekleform Report(IServiceProvider services)
    {
        var form = ActivatorUtilities.CreateInstance<Raporekleform>(services);
        SelectFirstUser(form, "comboBox1", services);
        Run(form.ShowSelectedUserAsync());
        return form;
    }

    private static TrendForm Trend(IServiceProvider services)
    {
        var form = ActivatorUtilities.CreateInstance<TrendForm>(services);
        SelectFirstUser(form, "userPicker", services); // Selecting a user loads the chart.
        return form;
    }

    private static HedefForm Goals(IServiceProvider services)
    {
        var form = ActivatorUtilities.CreateInstance<HedefForm>(services);
        SelectFirstUser(form, "userPicker", services);
        return form;
    }

    private static HatirlaticiForm Reminders(IServiceProvider services)
    {
        var form = ActivatorUtilities.CreateInstance<HatirlaticiForm>(services);
        Run(Find<ComboBox>(form, "userPicker").LoadUsersAsync(services.GetRequiredService<IUserService>()));
        Run(form.ReloadAsync());
        return form;
    }

    private static DonemRaporuForm PeriodReport(IServiceProvider services)
    {
        var form = ActivatorUtilities.CreateInstance<DonemRaporuForm>(services);
        // The "Son 30 gün" range; PerformClick does nothing on a window that is not shown.
        Find<DateTimePicker>(form, "fromPicker").Value = DateTime.Today.AddDays(-29);
        SelectFirstUser(form, "userPicker", services);
        return form;
    }

    private static void SelectFirstUser(Form form, string pickerName, IServiceProvider services)
    {
        var picker = Find<ComboBox>(form, pickerName);
        Run(picker.LoadUsersAsync(services.GetRequiredService<IUserService>()));
        picker.SelectedIndex = 0;
    }

    /// <summary>The in-memory services complete synchronously; anything else is a bug in the tool.</summary>
    private static void Run(Task task)
    {
        if (!task.IsCompleted)
        {
            throw new InvalidOperationException("A screen did not finish loading synchronously.");
        }

        task.GetAwaiter().GetResult();
    }

    internal static T Find<T>(Control root, string name)
        where T : Control =>
        root.Controls.Find(name, searchAllChildren: true).OfType<T>().SingleOrDefault()
        ?? throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Control '{0}' was not found.", name));

    /// <summary>The main menu never opens a screen while it is drawn.</summary>
    private sealed class UnusedFormFactory : IFormFactory
    {
        public T Create<T>()
            where T : Form => throw new InvalidOperationException("Screens are not opened while capturing.");
    }
}

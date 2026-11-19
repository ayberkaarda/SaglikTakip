using SaglikTakip.Business.Services;
using SaglikTakip.Domain.Entities;
using SaglikTakip.UI.Infrastructure;
using SaglikTakip.UI.Tests.Support;
using SaglikTakip.UI.Theming;
using static SaglikTakip.UI.Tests.Support.StaThread;

namespace SaglikTakip.UI.Tests;

public sealed class ReminderMonitorTests
{
    private static readonly DateTime Nine = new(2026, 1, 5, 9, 0, 0, DateTimeKind.Unspecified);

    private readonly ControlledReminderService _reminders = new();
    private readonly RecordingNotifier _notifier = new();

    [Fact]
    public async Task EachOccurrenceIsAnnouncedOnce()
    {
        var monitor = new ReminderMonitor(_reminders, _notifier);
        _reminders.Due.Add(ControlledReminderService.Item(1, Nine));

        Assert.Single(await monitor.CheckAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await monitor.CheckAsync(TestContext.Current.CancellationToken)); // Still due a minute later: no second balloon.

        // Marked as done elsewhere and due again at its next time: announced again.
        _reminders.Due.Clear();
        _reminders.Due.Add(ControlledReminderService.Item(1, Nine.AddDays(1)));
        Assert.Single(await monitor.CheckAsync(TestContext.Current.CancellationToken));

        Assert.Equal(2, _notifier.Batches.Count);
    }

    [Fact]
    public async Task OccurrencesListedAtStartup_AreNotAnnouncedAgain()
    {
        var monitor = new ReminderMonitor(_reminders, _notifier);
        var missed = ControlledReminderService.Item(1, Nine);
        _reminders.Due.Add(missed);
        _reminders.Due.Add(ControlledReminderService.Item(2, Nine.AddHours(1), "Yürüyüş"));

        monitor.MarkSeen([missed]);
        var announced = await monitor.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Equal([2], announced.Select(i => i.Reminder.Id));
        Assert.Equal("Yürüyüş", Assert.Single(Assert.Single(_notifier.Batches)).Reminder.Title);
    }

    [Fact]
    public async Task ACheckStartedWhileAnotherIsRunning_IsSkipped()
    {
        var monitor = new ReminderMonitor(_reminders, _notifier);
        _reminders.PendingDue = new TaskCompletionSource<IReadOnlyList<ReminderItem>>();

        var first = monitor.CheckAsync(TestContext.Current.CancellationToken);
        var second = await monitor.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Empty(second);
        Assert.Equal(1, _reminders.DueCalls);
        _reminders.PendingDue.SetResult([ControlledReminderService.Item(1, Nine)]);
        Assert.Single(await first);
    }

    [Fact]
    public async Task NoDueReminders_NoBalloon()
    {
        Assert.Empty(await new ReminderMonitor(_reminders, _notifier).CheckAsync(TestContext.Current.CancellationToken));
        Assert.Empty(_notifier.Batches);
    }

    [Fact]
    public void Describe_ShowsAtMostThreeLines()
    {
        var items = Enumerable.Range(1, 5).Select(i => ControlledReminderService.Item(i, Nine, "Ölç " + i)).ToList();

        var lines = ReminderMonitor.Describe(items).Split(Environment.NewLine);

        Assert.Equal(["Ali: Ölç 1 (Ölçüm)", "Ali: Ölç 2 (Ölçüm)", "Ali: Ölç 3 (Ölçüm)", "ve 2 hatırlatıcı daha"], lines);
    }

    private sealed class RecordingNotifier : IReminderNotifier
    {
        public List<IReadOnlyList<ReminderItem>> Batches { get; } = [];

        public void Notify(IReadOnlyList<ReminderItem> reminders) => Batches.Add(reminders);
    }
}

[CollectionDefinition("SharedTheme", DisableParallelization = true)]
public sealed class SharedThemeDefinition;

[Collection("SharedTheme")]
public sealed class HatirlaticiFormTests
{
    [Fact]
    public void DueStatus_HighContrastPreservesSelectionText() => Run(() =>
    {
        var previous = Theme.Current;
        Theme.Use(ThemePalette.HighContrast());
        try
        {
            var reminders = new ControlledReminderService();
            var due = ControlledReminderService.Item(1, new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Unspecified));
            reminders.Due.Add(due);
            reminders.Due.Add(due with { DueOccurrences = 3 });
            using var form = new HatirlaticiForm(new PendingUserService(), reminders);
            form.ReloadAsync().GetAwaiter().GetResult();
            var grid = Find<DataGridView>(form, "reminderGrid");
            var status = grid.Columns.Cast<DataGridViewColumn>().Single(c => c.DataPropertyName == "Status");

            Assert.Equal(2, grid.Rows.Count);
            foreach (DataGridViewRow row in grid.Rows)
            {
                var style = new DataGridViewCellStyle();
                form.HighlightStatus(grid, new DataGridViewCellFormattingEventArgs(status.Index, row.Index, null, typeof(string), style));

                Assert.Equal(Color.Empty, style.SelectionForeColor); // Inherited from the system selection colors.
                Assert.Same(Theme.Fonts.BodyStrong, style.Font); // The emphasis stays, as weight.
            }
        }
        finally
        {
            Theme.Use(previous);
        }
    });

    [Fact]
    public void Add_WhilePending_DoesNotInsertTwice() => Run(() =>
    {
        var reminders = new ControlledReminderService();
        using var form = new HatirlaticiForm(new PendingUserService(), reminders);
        var picker = Find<ComboBox>(form, "userPicker");
        picker.Items.Add(new User { Id = 1, Name = "Ali", Age = 40, Gender = Genders.Male });
        picker.SelectedIndex = 0;
        Find<TextBox>(form, "titleInput").Text = "Kilo ölç";

        var first = form.AddAsync();
        var second = form.AddAsync();

        Assert.Equal(1, reminders.AddCalls);
        Assert.False(first.IsCompleted);
        Assert.True(second.IsCompleted);
        Assert.False(Find<Button>(form, "btnEkle").Enabled);
    });

    [Fact]
    public void Complete_WhenDisplayedPendingReminderBecomesDue_UsesCurrentServiceState() => Run(() =>
    {
        var due = new DateTime(2026, 1, 5, 10, 0, 0, DateTimeKind.Unspecified);
        var reminders = new ControlledReminderService();
        var stale = ControlledReminderService.Item(7, due);
        reminders.Due.Add(new ReminderItem(stale.Reminder, "Ali", false, 0)); // Loaded at 09:59: not due yet.
        using var form = new HatirlaticiForm(new PendingUserService(), reminders);
        _ = form.ReloadAsync();
        Assert.Equal(1, reminders.GetAllCalls);
        form.SelectionOverride = reminders.Due[0];

        // The clock passes 10:00 while the screen stays open; the service now reports the row as due.
        reminders.Due[0] = stale;
        form.SelectionOverride = new ReminderItem(stale.Reminder, "Ali", false, 0); // Still the stale row on screen.
        var complete = form.CompleteSelectedAsync();

        Assert.True(complete.IsCompletedSuccessfully);
        Assert.Equal([(7, due)], reminders.Completed);
        Assert.Equal(2, reminders.GetAllCalls); // The list is refreshed after the attempt.
    });

    [Fact]
    public void IntervalUnits_AreConvertedToHours() => Run(() =>
    {
        using var form = new HatirlaticiForm(new PendingUserService(), new ControlledReminderService());
        Find<NumericUpDown>(form, "everyInput").Value = 2;
        var unit = Find<ComboBox>(form, "unitPicker");

        unit.SelectedIndex = 0;
        Assert.Equal(2, form.IntervalHours);
        unit.SelectedIndex = 1;
        Assert.Equal(48, form.IntervalHours);
        unit.SelectedIndex = 2;
        Assert.Equal(336, form.IntervalHours);
    });

    [Theory]
    [InlineData(5, "Her 5 saat")]
    [InlineData(48, "Her 2 gün")]
    [InlineData(168, "Her 1 hafta")]
    [InlineData(8760, "Her 365 gün")]
    public void DescribeInterval(int hours, string expected)
    {
        Assert.Equal(expected, HatirlaticiForm.DescribeInterval(hours));
    }

    [Fact]
    public void DescribeStatus_ShowsMissedCount()
    {
        var reminder = new Reminder { Id = 1, Title = "x", IntervalHours = 24 };

        Assert.Equal("Bekliyor", HatirlaticiForm.DescribeStatus(new ReminderItem(reminder, "Ali", false, 0)));
        Assert.Equal("Zamanı geldi", HatirlaticiForm.DescribeStatus(new ReminderItem(reminder, "Ali", true, 1)));
        Assert.Equal("Zamanı geçti (3 kez)", HatirlaticiForm.DescribeStatus(new ReminderItem(reminder, "Ali", true, 3)));
    }
}

public sealed class Form1ReminderTests
{
    private static readonly DateTime Nine = new(2026, 1, 5, 9, 0, 0, DateTimeKind.Unspecified);

    [Fact]
    public void Startup_WhenAnotherDialogIsOpen_DoesNotConsumeUnshownReminders() => Run(() =>
    {
        var reminders = new ControlledReminderService { PendingDue = new TaskCompletionSource<IReadOnlyList<ReminderItem>>() };
        var notifier = new RecordingNotifier();
        var opened = new List<Type>();
        using var form = NewForm(reminders, notifier, opened);

        var startup = form.StartRemindersAsync();
        var overdue = ControlledReminderService.Item(1, Nine);

        // The user opens another screen; the startup answer arrives while it is still open.
        form.DialogHook = type =>
        {
            opened.Add(type);
            reminders.PendingDue.SetResult([overdue]);
        };
        form.OpenDialog<HedefForm>();
        Assert.True(startup.IsCompletedSuccessfully);
        Assert.Equal([typeof(HedefForm)], opened); // The reminder list was not shown.

        // The screen is closed; the next periodic check must still announce the reminder.
        reminders.PendingDue = null;
        reminders.Due.Add(overdue);
        form.CheckRemindersAsync().GetAwaiter().GetResult();

        Assert.Single(Assert.Single(notifier.Batches));
    });

    [Fact]
    public void Startup_WhenListIsShown_MarksRemindersAsSeen() => Run(() =>
    {
        var reminders = new ControlledReminderService();
        reminders.Due.Add(ControlledReminderService.Item(1, Nine));
        var notifier = new RecordingNotifier();
        var opened = new List<Type>();
        using var form = NewForm(reminders, notifier, opened);

        form.StartRemindersAsync().GetAwaiter().GetResult();
        form.CheckRemindersAsync().GetAwaiter().GetResult();

        Assert.Equal([typeof(HatirlaticiForm)], opened);
        Assert.Empty(notifier.Batches);
    });

    [Fact]
    public void Startup_AfterTheFormIsDisposed_TouchesNothing() => Run(() =>
    {
        var reminders = new ControlledReminderService { PendingDue = new TaskCompletionSource<IReadOnlyList<ReminderItem>>() };
        var notifier = new RecordingNotifier();
        var opened = new List<Type>();
        var form = NewForm(reminders, notifier, opened);

        var startup = form.StartRemindersAsync();
        form.Dispose();
        reminders.PendingDue.SetResult([ControlledReminderService.Item(1, Nine)]);

        Assert.True(startup.IsCompleted);
        Assert.Empty(opened);
        Assert.True(notifier.Disposed);
    });

    [Fact]
    public void PeriodicCheck_AfterTheFormIsDisposed_DoesNotNotify() => Run(() =>
    {
        var reminders = new ControlledReminderService();
        var notifier = new RecordingNotifier();
        var form = NewForm(reminders, notifier, []);
        form.StartRemindersAsync().GetAwaiter().GetResult();

        reminders.PendingDue = new TaskCompletionSource<IReadOnlyList<ReminderItem>>();
        var check = form.CheckRemindersAsync();
        form.Dispose();
        reminders.PendingDue.SetResult([ControlledReminderService.Item(1, Nine)]);

        Assert.True(check.IsCompleted);
        Assert.Empty(notifier.Batches);
    });

    private static Form1 NewForm(ControlledReminderService reminders, RecordingNotifier notifier, List<Type> opened) =>
        new(new UnusedFormFactory(), reminders)
        {
            NotifierFactory = _ => notifier,
            DialogHook = opened.Add,
        };

    private sealed class UnusedFormFactory : IFormFactory
    {
        public T Create<T>()
            where T : Form => throw new InvalidOperationException("Dialogs are replaced by the test hook.");
    }

    private sealed class RecordingNotifier : IReminderNotifier, IDisposable
    {
        public List<IReadOnlyList<ReminderItem>> Batches { get; } = [];

        public bool Disposed { get; private set; }

        public void Notify(IReadOnlyList<ReminderItem> reminders) => Batches.Add(reminders);

        public void Dispose() => Disposed = true;
    }
}

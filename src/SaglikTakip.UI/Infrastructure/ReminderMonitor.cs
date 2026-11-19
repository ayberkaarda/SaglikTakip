using System.Globalization;
using SaglikTakip.Business.Services;
using SaglikTakip.Domain.Entities;
using Serilog;

namespace SaglikTakip.UI.Infrastructure;

/// <summary>Shows due reminders to the user without opening a window.</summary>
internal interface IReminderNotifier
{
    void Notify(IReadOnlyList<ReminderItem> reminders);
}

/// <summary>
/// Checks for due reminders while the application is open. Each occurrence (reminder and due time) is
/// announced once per session; marking it as done gives it a new due time, which is announced again
/// when it comes due. A check that starts while another is still running is skipped.
/// </summary>
internal sealed class ReminderMonitor(IReminderService reminders, IReminderNotifier notifier)
{
    private readonly HashSet<(int Id, DateTime DueAt)> _announced = [];
    private bool _checking;

    /// <summary>Records occurrences the user has already seen (for example in the startup list).</summary>
    public void MarkSeen(IEnumerable<ReminderItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        foreach (var item in items)
        {
            _announced.Add((item.Reminder.Id, item.Reminder.NextDueAt));
        }
    }

    /// <summary>Announces due occurrences that were not announced yet; returns them.</summary>
    public async Task<IReadOnlyList<ReminderItem>> CheckAsync()
    {
        if (_checking)
        {
            return [];
        }

        _checking = true;
        try
        {
            var due = await reminders.GetDueAsync();
            var fresh = due.Where(item => _announced.Add((item.Reminder.Id, item.Reminder.NextDueAt))).ToList();
            if (fresh.Count > 0)
            {
                // Only the count is logged; titles and names can describe a person's health.
                Log.Information("Announcing {Count} due reminder(s)", fresh.Count);
                notifier.Notify(fresh);
            }

            return fresh;
        }
        finally
        {
            _checking = false;
        }
    }

    /// <summary>Balloon text, for example "Ali: Tansiyon ölç (Ölçüm)"; at most three lines plus a remainder.</summary>
    internal static string Describe(IReadOnlyList<ReminderItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        const int MaxLines = 3;
        var lines = items.Take(MaxLines).Select(i => string.Format(
            CultureInfo.CurrentCulture, "{0}: {1} ({2})", i.UserName, i.Reminder.Title, KindText(i.Reminder.Kind)));
        var text = string.Join(Environment.NewLine, lines);
        return items.Count > MaxLines
            ? text + Environment.NewLine + string.Format(CultureInfo.CurrentCulture, "ve {0} hatırlatıcı daha", items.Count - MaxLines)
            : text;
    }

    internal static string KindText(ReminderKind kind) => kind == ReminderKind.Exercise ? "Egzersiz" : "Ölçüm";
}

/// <summary>
/// Shows reminders as a balloon from the notification area icon. A balloon never takes the focus or
/// brings a window to the front; clicking it raises <see cref="Clicked"/>.
/// </summary>
internal sealed class NotifyIconReminderNotifier : IReminderNotifier, IDisposable
{
    private readonly NotifyIcon _icon;

    public NotifyIconReminderNotifier(Icon? icon)
    {
        _icon = new NotifyIcon
        {
            Icon = icon ?? SystemIcons.Application,
            Text = "Sağlık Takip",
            Visible = true,
        };
        _icon.BalloonTipClicked += (_, _) => Clicked?.Invoke(this, EventArgs.Empty);
        _icon.DoubleClick += (_, _) => Clicked?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Clicked;

    public void Notify(IReadOnlyList<ReminderItem> reminders) =>
        _icon.ShowBalloonTip(10_000, "Sağlık Takip | Hatırlatıcı", ReminderMonitor.Describe(reminders), ToolTipIcon.Info);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}

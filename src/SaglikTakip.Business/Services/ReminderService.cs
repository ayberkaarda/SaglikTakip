using FluentValidation;
using SaglikTakip.Business.Insights;
using SaglikTakip.Business.Validation;
using SaglikTakip.Domain.Entities;
using SaglikTakip.Domain.Repositories;

namespace SaglikTakip.Business.Services;

/// <summary>A reminder with its user's display name and its state at the time it was read.</summary>
public sealed record ReminderItem(Reminder Reminder, string UserName, bool IsDue, int DueOccurrences);

public interface IReminderService
{
    /// <summary>All reminders, earliest due first.</summary>
    Task<IReadOnlyList<ReminderItem>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Reminders due now, including the ones that came due while the application was closed.</summary>
    Task<IReadOnlyList<ReminderItem>> GetDueAsync(CancellationToken cancellationToken = default);

    Task<Reminder> AddAsync(
        int userId,
        ReminderKind kind,
        string? title,
        int intervalHours,
        DateTime firstDueAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the occurrence due at <paramref name="expectedDueAt"/> as done and moves the reminder to
    /// its next time after now. Returns <see langword="false"/> when the reminder is gone, not due, or
    /// that occurrence was already marked (for example by a second click).
    /// </summary>
    Task<bool> CompleteAsync(int id, DateTime expectedDueAt, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}

internal sealed class ReminderService(
    IUserRepository users,
    IReminderRepository reminders,
    IValidator<ReminderInput> validator,
    TimeProvider timeProvider) : IReminderService
{
    public async Task<IReadOnlyList<ReminderItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var all = await reminders.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return await DescribeAsync(all, Now(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ReminderItem>> GetDueAsync(CancellationToken cancellationToken = default)
    {
        var now = Now();
        var due = await reminders.GetDueAsync(now, cancellationToken).ConfigureAwait(false);
        return await DescribeAsync(due, now, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Reminder> AddAsync(
        int userId,
        ReminderKind kind,
        string? title,
        int intervalHours,
        DateTime firstDueAt,
        CancellationToken cancellationToken = default)
    {
        var input = ReminderInput.Create(userId, kind, title, intervalHours, firstDueAt);
        validator.ValidateOrThrow(input);

        await UserGuard.EnsureExistsAsync(users, userId, cancellationToken).ConfigureAwait(false);

        var reminder = new Reminder
        {
            UserId = input.UserId,
            Kind = input.Kind,
            Title = input.Title,
            IntervalHours = input.IntervalHours,
            NextDueAt = input.FirstDueAt,
        };
        reminder.Id = await reminders.AddAsync(reminder, cancellationToken).ConfigureAwait(false);
        return reminder;
    }

    public async Task<bool> CompleteAsync(int id, DateTime expectedDueAt, CancellationToken cancellationToken = default)
    {
        var reminder = await reminders.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        var now = Now();
        if (reminder is null || reminder.NextDueAt != expectedDueAt || !ReminderSchedule.IsDue(reminder.NextDueAt, now))
        {
            return false;
        }

        var next = ReminderSchedule.NextAfter(reminder.NextDueAt, reminder.IntervalHours, now);
        return await reminders.RescheduleAsync(id, expectedDueAt, next, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) =>
        reminders.DeleteAsync(id, cancellationToken);

    private DateTime Now() => timeProvider.GetLocalNow().DateTime;

    private async Task<IReadOnlyList<ReminderItem>> DescribeAsync(
        IReadOnlyList<Reminder> list,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (list.Count == 0)
        {
            return [];
        }

        var names = (await users.GetAllAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(u => u.Id, u => u.Name);
        return list
            .Select(r => new ReminderItem(
                r,
                names.GetValueOrDefault(r.UserId, string.Empty),
                ReminderSchedule.IsDue(r.NextDueAt, now),
                ReminderSchedule.DueOccurrences(r.NextDueAt, r.IntervalHours, now)))
            .ToList();
    }
}

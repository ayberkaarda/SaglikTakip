using Microsoft.EntityFrameworkCore;
using SaglikTakip.Data.Persistence;
using SaglikTakip.Domain.Entities;
using SaglikTakip.Domain.Repositories;

namespace SaglikTakip.Data.Repositories;

internal sealed class ReminderRepository(IDbContextFactory<HealthDbContext> contextFactory) : IReminderRepository
{
    public async Task<IReadOnlyList<Reminder>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Reminders
            .AsNoTracking()
            .OrderBy(r => r.NextDueAt)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Reminder>> GetDueAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Reminders
            .AsNoTracking()
            .Where(r => r.NextDueAt <= now)
            .OrderBy(r => r.NextDueAt)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Reminder?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Reminders
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> AddAsync(Reminder reminder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reminder);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = new Reminder
        {
            UserId = reminder.UserId,
            Kind = reminder.Kind,
            Title = reminder.Title,
            IntervalHours = reminder.IntervalHours,
            NextDueAt = reminder.NextDueAt,
        };
        db.Reminders.Add(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity.Id;
    }

    public async Task<bool> RescheduleAsync(
        int id,
        DateTime expectedDueAt,
        DateTime nextDueAt,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Conditional update: a second "done" for the same occurrence finds a different due time and changes nothing.
        var updated = await db.Reminders
            .Where(r => r.Id == id && r.NextDueAt == expectedDueAt)
            .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.NextDueAt, nextDueAt), cancellationToken)
            .ConfigureAwait(false);
        return updated > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var deleted = await db.Reminders
            .Where(r => r.Id == id)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        return deleted > 0;
    }
}

using SaglikTakip.Domain.Entities;

namespace SaglikTakip.Domain.Repositories;

public interface IReminderRepository
{
    /// <summary>All reminders, earliest due first.</summary>
    Task<IReadOnlyList<Reminder>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Reminders due at or before <paramref name="now"/>, earliest first.</summary>
    Task<IReadOnlyList<Reminder>> GetDueAsync(DateTime now, CancellationToken cancellationToken = default);

    Task<Reminder?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<int> AddAsync(Reminder reminder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves the reminder to <paramref name="nextDueAt"/> only while it is still due at
    /// <paramref name="expectedDueAt"/>, so a repeated or concurrent "done" cannot skip an interval.
    /// </summary>
    Task<bool> RescheduleAsync(int id, DateTime expectedDueAt, DateTime nextDueAt, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}

using SaglikTakip.Domain.Entities;
using SaglikTakip.Domain.Repositories;

namespace SaglikTakip.Screenshots;

// Repositories over lists, so the real business services run without SQL Server.

internal sealed class InMemoryUserRepository : IUserRepository
{
    public List<User> Users { get; } = [];

    public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<User>>(Users.OrderBy(u => u.Id).ToList());

    public Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.SingleOrDefault(u => u.Id == id));

    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.Any(u => u.Id == id));

    public Task<int> AddAsync(User user, CancellationToken cancellationToken = default)
    {
        user.Id = Users.Count == 0 ? 1 : Users.Max(u => u.Id) + 1;
        Users.Add(user);
        return Task.FromResult(user.Id);
    }
}

internal sealed class InMemoryHealthRecordRepository : IHealthRecordRepository
{
    public List<HealthRecord> Records { get; } = [];

    public Task<IReadOnlyList<HealthRecord>> GetByUserAsync(int userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<HealthRecord>>(Records
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.Id)
            .ToList());

    public Task<IReadOnlyList<HealthRecord>> GetByUserBetweenAsync(
        int userId,
        DateTime firstDay,
        DateTime lastDay,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<HealthRecord>>(Records
            .Where(r => r.UserId == userId && r.Date.Date >= firstDay.Date && r.Date.Date <= lastDay.Date)
            .OrderBy(r => r.Date)
            .ThenBy(r => r.Id)
            .ToList());

    public Task<int> AddAsync(HealthRecord record, CancellationToken cancellationToken = default)
    {
        record.Id = Records.Count + 1;
        Records.Add(record);
        return Task.FromResult(record.Id);
    }
}

internal sealed class InMemoryExerciseRepository : IExerciseRepository
{
    public List<Exercise> Exercises { get; } = [];

    public Task<IReadOnlyList<Exercise>> GetByUserAsync(int userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Exercise>>(Exercises
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.Id)
            .ToList());

    public Task<int> AddAsync(Exercise exercise, CancellationToken cancellationToken = default)
    {
        exercise.Id = Exercises.Count + 1;
        Exercises.Add(exercise);
        return Task.FromResult(exercise.Id);
    }
}

internal sealed class InMemoryHealthGoalRepository : IHealthGoalRepository
{
    public List<HealthGoal> Goals { get; } = [];

    public Task<HealthGoal?> GetByUserAsync(int userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Goals.SingleOrDefault(g => g.UserId == userId));

    public Task<int> SaveAsync(HealthGoal goal, CancellationToken cancellationToken = default)
    {
        Goals.RemoveAll(g => g.UserId == goal.UserId);
        goal.Id = Goals.Count + 1;
        Goals.Add(goal);
        return Task.FromResult(goal.Id);
    }

    public Task<bool> DeleteByUserAsync(int userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Goals.RemoveAll(g => g.UserId == userId) > 0);
}

internal sealed class InMemoryReminderRepository : IReminderRepository
{
    public List<Reminder> Reminders { get; } = [];

    public Task<IReadOnlyList<Reminder>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Reminder>>(Reminders.OrderBy(r => r.NextDueAt).ThenBy(r => r.Id).ToList());

    public Task<IReadOnlyList<Reminder>> GetDueAsync(DateTime now, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Reminder>>(Reminders.Where(r => r.NextDueAt <= now).OrderBy(r => r.NextDueAt).ToList());

    public Task<Reminder?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Reminders.SingleOrDefault(r => r.Id == id));

    public Task<int> AddAsync(Reminder reminder, CancellationToken cancellationToken = default)
    {
        reminder.Id = Reminders.Count + 1;
        Reminders.Add(reminder);
        return Task.FromResult(reminder.Id);
    }

    public Task<bool> RescheduleAsync(int id, DateTime expectedDueAt, DateTime nextDueAt, CancellationToken cancellationToken = default)
    {
        var reminder = Reminders.SingleOrDefault(r => r.Id == id && r.NextDueAt == expectedDueAt);
        if (reminder is not null)
        {
            reminder.NextDueAt = nextDueAt;
        }

        return Task.FromResult(reminder is not null);
    }

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Reminders.RemoveAll(r => r.Id == id) > 0);
}

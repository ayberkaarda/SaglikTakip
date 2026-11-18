using SaglikTakip.Domain.Entities;
using SaglikTakip.Domain.Repositories;

namespace SaglikTakip.Business.Tests;

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
        var id = Users.Count == 0 ? 1 : Users.Max(u => u.Id) + 1;
        Users.Add(new User { Id = id, Name = user.Name, Age = user.Age, Gender = user.Gender });
        return Task.FromResult(id);
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

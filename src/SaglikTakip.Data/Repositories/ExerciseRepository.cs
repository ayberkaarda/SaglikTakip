using Microsoft.EntityFrameworkCore;
using SaglikTakip.Data.Persistence;
using SaglikTakip.Domain.Entities;
using SaglikTakip.Domain.Repositories;

namespace SaglikTakip.Data.Repositories;

internal sealed class ExerciseRepository(IDbContextFactory<HealthDbContext> contextFactory) : IExerciseRepository
{
    public async Task<IReadOnlyList<Exercise>> GetByUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Exercises
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> AddAsync(Exercise exercise, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = new Exercise
        {
            UserId = exercise.UserId,
            Name = exercise.Name,
            DurationMinutes = exercise.DurationMinutes,
            Repetitions = exercise.Repetitions,
        };
        db.Exercises.Add(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity.Id;
    }
}

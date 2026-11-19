using Microsoft.EntityFrameworkCore;
using SaglikTakip.Data.Persistence;
using SaglikTakip.Domain.Entities;
using SaglikTakip.Domain.Repositories;

namespace SaglikTakip.Data.Repositories;

internal sealed class HealthGoalRepository(IDbContextFactory<HealthDbContext> contextFactory) : IHealthGoalRepository
{
    public async Task<HealthGoal?> GetByUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.HealthGoals
            .AsNoTracking()
            .SingleOrDefaultAsync(g => g.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> SaveAsync(HealthGoal goal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(goal);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // A set-based update replaces every value in one statement; a new row is added only when the
        // user has no goal yet (the unique index on KullaniciId refuses a second one).
        var updated = await db.HealthGoals
            .Where(g => g.UserId == goal.UserId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(g => g.TargetWeight, goal.TargetWeight)
                    .SetProperty(g => g.StartWeight, goal.StartWeight)
                    .SetProperty(g => g.MinWeight, goal.MinWeight)
                    .SetProperty(g => g.MaxWeight, goal.MaxWeight)
                    .SetProperty(g => g.MinPulse, goal.MinPulse)
                    .SetProperty(g => g.MaxPulse, goal.MaxPulse),
                cancellationToken)
            .ConfigureAwait(false);

        if (updated > 0)
        {
            return await db.HealthGoals
                .Where(g => g.UserId == goal.UserId)
                .Select(g => g.Id)
                .SingleAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var entity = new HealthGoal
        {
            UserId = goal.UserId,
            TargetWeight = goal.TargetWeight,
            StartWeight = goal.StartWeight,
            MinWeight = goal.MinWeight,
            MaxWeight = goal.MaxWeight,
            MinPulse = goal.MinPulse,
            MaxPulse = goal.MaxPulse,
        };
        db.HealthGoals.Add(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity.Id;
    }

    public async Task<bool> DeleteByUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var deleted = await db.HealthGoals
            .Where(g => g.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        return deleted > 0;
    }
}

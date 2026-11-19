using SaglikTakip.Domain.Entities;

namespace SaglikTakip.Domain.Repositories;

public interface IHealthGoalRepository
{
    Task<HealthGoal?> GetByUserAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Creates the user's goal or replaces all of its values. Returns the goal id.</summary>
    Task<int> SaveAsync(HealthGoal goal, CancellationToken cancellationToken = default);

    /// <summary>Deletes the user's goal; <see langword="false"/> when there was none.</summary>
    Task<bool> DeleteByUserAsync(int userId, CancellationToken cancellationToken = default);
}

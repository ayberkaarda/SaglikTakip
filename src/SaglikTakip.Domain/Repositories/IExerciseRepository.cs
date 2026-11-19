using SaglikTakip.Domain.Entities;

namespace SaglikTakip.Domain.Repositories;

public interface IExerciseRepository
{
    /// <summary>Exercises of the user, newest entry first.</summary>
    Task<IReadOnlyList<Exercise>> GetByUserAsync(int userId, CancellationToken cancellationToken = default);

    Task<int> AddAsync(Exercise exercise, CancellationToken cancellationToken = default);
}

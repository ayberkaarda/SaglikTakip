using SaglikTakip.Domain.Entities;

namespace SaglikTakip.Domain.Repositories;

public interface IHealthRecordRepository
{
    /// <summary>Records of the user, newest date first (ties: newest entry first).</summary>
    Task<IReadOnlyList<HealthRecord>> GetByUserAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records of the user dated from <paramref name="firstDay"/> to <paramref name="lastDay"/>, both days included
    /// (time parts are ignored), oldest date first (ties: oldest entry first).
    /// </summary>
    Task<IReadOnlyList<HealthRecord>> GetByUserBetweenAsync(
        int userId,
        DateTime firstDay,
        DateTime lastDay,
        CancellationToken cancellationToken = default);

    Task<int> AddAsync(HealthRecord record, CancellationToken cancellationToken = default);
}

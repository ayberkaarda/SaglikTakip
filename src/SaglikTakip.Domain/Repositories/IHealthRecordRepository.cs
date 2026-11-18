using SaglikTakip.Domain.Entities;

namespace SaglikTakip.Domain.Repositories;

public interface IHealthRecordRepository
{
    /// <summary>Records of the user, newest date first (ties: newest entry first).</summary>
    Task<IReadOnlyList<HealthRecord>> GetByUserAsync(int userId, CancellationToken cancellationToken = default);

    Task<int> AddAsync(HealthRecord record, CancellationToken cancellationToken = default);
}

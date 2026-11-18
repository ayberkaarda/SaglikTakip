using SaglikTakip.Domain.Entities;

namespace SaglikTakip.Domain.Repositories;

public interface IUserRepository
{
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);

    Task<int> AddAsync(User user, CancellationToken cancellationToken = default);
}

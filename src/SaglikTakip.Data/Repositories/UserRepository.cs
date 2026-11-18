using Microsoft.EntityFrameworkCore;
using SaglikTakip.Data.Persistence;
using SaglikTakip.Domain.Entities;
using SaglikTakip.Domain.Repositories;

namespace SaglikTakip.Data.Repositories;

internal sealed class UserRepository(IDbContextFactory<HealthDbContext> contextFactory) : IUserRepository
{
    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Users
            .AsNoTracking()
            .OrderBy(u => u.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Users.AnyAsync(u => u.Id == id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> AddAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = new User { Name = user.Name, Age = user.Age, Gender = user.Gender };
        db.Users.Add(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity.Id;
    }
}

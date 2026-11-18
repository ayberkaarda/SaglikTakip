using Microsoft.EntityFrameworkCore;
using SaglikTakip.Data.Persistence;
using SaglikTakip.Domain.Entities;
using SaglikTakip.Domain.Repositories;

namespace SaglikTakip.Data.Repositories;

internal sealed class HealthRecordRepository(IDbContextFactory<HealthDbContext> contextFactory) : IHealthRecordRepository
{
    public async Task<IReadOnlyList<HealthRecord>> GetByUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.HealthRecords
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> AddAsync(HealthRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = new HealthRecord
        {
            UserId = record.UserId,
            Date = record.Date,
            Weight = record.Weight,
            Pulse = record.Pulse,
            Notes = record.Notes,
        };
        db.HealthRecords.Add(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity.Id;
    }
}

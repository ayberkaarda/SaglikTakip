using FluentValidation;
using SaglikTakip.Business.Validation;
using SaglikTakip.Domain.Entities;
using SaglikTakip.Domain.Repositories;

namespace SaglikTakip.Business.Services;

public interface IHealthRecordService
{
    Task<HealthRecord> AddAsync(
        int userId,
        DateTime recordDate,
        decimal weight,
        int pulse,
        string? notes,
        CancellationToken cancellationToken = default);
}

internal sealed class HealthRecordService(
    IUserRepository users,
    IHealthRecordRepository records,
    IValidator<HealthRecordInput> validator) : IHealthRecordService
{
    public async Task<HealthRecord> AddAsync(
        int userId,
        DateTime recordDate,
        decimal weight,
        int pulse,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var input = HealthRecordInput.Create(userId, recordDate, weight, pulse, notes);
        validator.ValidateOrThrow(input);

        await UserGuard.EnsureExistsAsync(users, userId, cancellationToken).ConfigureAwait(false);

        var record = new HealthRecord
        {
            UserId = input.UserId,
            Date = input.Date,
            Weight = (double)input.Weight,
            Pulse = input.Pulse,
            Notes = input.Notes,
        };

        record.Id = await records.AddAsync(record, cancellationToken).ConfigureAwait(false);
        return record;
    }
}

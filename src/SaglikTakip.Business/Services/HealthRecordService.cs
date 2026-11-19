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
    TimeProvider timeProvider) : IHealthRecordService
{
    internal const decimal MinWeight = 1;
    internal const decimal MaxWeight = 200;
    internal const int MinPulse = 1;
    internal const int MaxPulse = 250;
    internal const int MaxNotesLength = 500;
    internal static readonly DateTime MinDate = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    public async Task<HealthRecord> AddAsync(
        int userId,
        DateTime recordDate,
        decimal weight,
        int pulse,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var record = new HealthRecord
        {
            UserId = userId,
            Date = ValidateDate(recordDate),
            Weight = (double)Validation.Range(weight, "Kilo", MinWeight, MaxWeight),
            Pulse = Validation.Range(pulse, "Nabız", MinPulse, MaxPulse),
            Notes = Validation.Optional(notes, "Notlar", MaxNotesLength),
        };

        await UserGuard.EnsureExistsAsync(users, userId, cancellationToken).ConfigureAwait(false);

        record.Id = await records.AddAsync(record, cancellationToken).ConfigureAwait(false);
        return record;
    }

    private DateTime ValidateDate(DateTime date)
    {
        var day = date.Date;
        if (day < MinDate)
        {
            throw new BusinessRuleException("Lütfen geçerli bir tarih seçin.");
        }

        if (day > timeProvider.GetLocalNow().Date)
        {
            throw new BusinessRuleException("Sağlık kaydının tarihi ileri bir tarih olamaz.");
        }

        return day;
    }
}

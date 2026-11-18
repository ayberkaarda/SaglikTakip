using SaglikTakip.Domain.Entities;
using SaglikTakip.Domain.Repositories;

namespace SaglikTakip.Business.Services;

public interface IReportService
{
    /// <summary>Returns the health records and exercises of a user, newest first.</summary>
    Task<UserReport> GetUserReportAsync(int userId, CancellationToken cancellationToken = default);
}

internal sealed class ReportService(
    IUserRepository users,
    IHealthRecordRepository records,
    IExerciseRepository exercises) : IReportService
{
    public async Task<UserReport> GetUserReportAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessRuleException("Seçilen kullanıcı bulunamadı.");

        return new UserReport
        {
            User = user,
            HealthRecords = await records.GetByUserAsync(userId, cancellationToken).ConfigureAwait(false),
            Exercises = await exercises.GetByUserAsync(userId, cancellationToken).ConfigureAwait(false),
        };
    }
}

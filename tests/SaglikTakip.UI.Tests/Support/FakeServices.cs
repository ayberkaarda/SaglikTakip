using SaglikTakip.Business.Insights;
using SaglikTakip.Business.Services;
using SaglikTakip.Domain.Entities;

namespace SaglikTakip.UI.Tests.Support;

/// <summary>Service doubles whose writes never complete, so a save stays pending.</summary>
internal sealed class PendingUserService : IUserService
{
    private readonly TaskCompletionSource<User> _pending = new();

    public int AddCalls { get; private set; }

    public List<User> Users { get; } = [];

    public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<User>>(Users);

    public Task<User> AddAsync(string? name, int age, string? gender, CancellationToken cancellationToken = default)
    {
        AddCalls++;
        return _pending.Task;
    }
}

internal sealed class PendingExerciseService : IExerciseService
{
    private readonly TaskCompletionSource<Exercise> _pending = new();

    public int AddCalls { get; private set; }

    public Task<Exercise> AddAsync(
        int userId,
        string? name,
        int durationMinutes,
        int repetitions,
        CancellationToken cancellationToken = default)
    {
        AddCalls++;
        return _pending.Task;
    }
}

internal sealed class PendingHealthRecordService : IHealthRecordService
{
    private readonly TaskCompletionSource<HealthRecord> _pending = new();

    public int AddCalls { get; private set; }

    public Task<HealthRecord> AddAsync(
        int userId,
        DateTime recordDate,
        decimal weight,
        int pulse,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        AddCalls++;
        return _pending.Task;
    }
}

/// <summary>Report service whose responses are completed by the test, in any order.</summary>
internal sealed class ControlledReportService : IReportService
{
    private readonly Dictionary<int, TaskCompletionSource<UserReport>> _requests = [];

    public Task<UserReport> GetUserReportAsync(int userId, CancellationToken cancellationToken = default)
    {
        var request = new TaskCompletionSource<UserReport>();
        _requests[userId] = request;
        return request.Task;
    }

    public void Respond(User user, double weight) =>
        _requests[user.Id].SetResult(new UserReport
        {
            User = user,
            HealthRecords = [new HealthRecord { UserId = user.Id, Date = new DateTime(2026, 1, 2), Weight = weight, Pulse = 70 }],
            Exercises = [],
        });
}

/// <summary>Trend service whose responses are completed by the test, in any order.</summary>
internal sealed class ControlledTrendService : ITrendService
{
    private readonly Dictionary<int, TaskCompletionSource<MetricTrend>> _requests = [];

    public int Calls { get; private set; }

    public Task<MetricTrend> GetTrendAsync(
        int userId,
        HealthMetric metric,
        DateTime firstDay,
        DateTime lastDay,
        int movingAverageWindow,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        var request = new TaskCompletionSource<MetricTrend>();
        _requests[userId] = request;
        return request.Task;
    }

    public void Respond(int userId, params double[] values) =>
        _requests[userId].SetResult(new MetricTrend
        {
            Metric = HealthMetric.Weight,
            From = new DateTime(2026, 1, 1),
            To = new DateTime(2026, 1, 31),
            MovingAverageWindow = 1,
            Points = values.Select((v, i) => new TrendPoint(new DateTime(2026, 1, 1).AddDays(i), v, 1, v)).ToList(),
            Summary = values.Length == 0 ? null : new MetricSummary(values.Length, values.Min(), values.Max(), values.Average(), values[0], values[^1]),
        });
}

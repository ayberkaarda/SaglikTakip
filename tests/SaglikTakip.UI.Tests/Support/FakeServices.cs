using SaglikTakip.Business.Insights;
using SaglikTakip.Business.Reporting;
using SaglikTakip.Business.Services;
using SaglikTakip.Business.Validation;
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

/// <summary>
/// Goal service whose status answers are completed by the test (or immediately with "no goal" when
/// <see cref="AnswerImmediately"/> is set) and whose writes never complete.
/// </summary>
internal sealed class ControlledGoalService : IGoalService
{
    private readonly Dictionary<int, TaskCompletionSource<GoalStatus?>> _requests = [];
    private readonly TaskCompletionSource<HealthGoal> _pendingSave = new();

    public bool AnswerImmediately { get; init; }

    public int SaveCalls { get; private set; }

    public List<(int UserId, GoalInput Input)> Saved { get; } = [];

    public Task<HealthGoal?> GetAsync(int userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<HealthGoal?>(null);

    public Task<HealthGoal> SaveAsync(int userId, GoalInput input, CancellationToken cancellationToken = default)
    {
        SaveCalls++;
        Saved.Add((userId, input));
        return _pendingSave.Task;
    }

    public Task<bool> DeleteAsync(int userId, CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task<GoalStatus?> GetStatusAsync(int userId, DateTime firstDay, DateTime lastDay, CancellationToken cancellationToken = default)
    {
        if (AnswerImmediately)
        {
            return Task.FromResult<GoalStatus?>(null);
        }

        var request = new TaskCompletionSource<GoalStatus?>();
        _requests[userId] = request;
        return request.Task;
    }

    public Task<IReadOnlyList<ThresholdAlert>> CheckAsync(HealthRecord record, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ThresholdAlert>>([]);

    public void Respond(int userId, double targetWeight) =>
        _requests[userId].SetResult(new GoalStatus
        {
            Goal = new HealthGoal { UserId = userId, TargetWeight = targetWeight },
            CurrentWeight = null,
            CurrentWeightDate = null,
            ProgressPercent = null,
            Alerts = [],
        });
}

/// <summary>Reminder service with a settable due list and writes that never complete.</summary>
internal sealed class ControlledReminderService : IReminderService
{
    private readonly TaskCompletionSource<Reminder> _pendingAdd = new();

    public List<ReminderItem> Due { get; } = [];

    public int AddCalls { get; private set; }

    public int DueCalls { get; private set; }

    public TaskCompletionSource<IReadOnlyList<ReminderItem>>? PendingDue { get; set; }

    public Task<IReadOnlyList<ReminderItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ReminderItem>>(Due.ToList());

    public Task<IReadOnlyList<ReminderItem>> GetDueAsync(CancellationToken cancellationToken = default)
    {
        DueCalls++;
        return PendingDue?.Task ?? Task.FromResult<IReadOnlyList<ReminderItem>>(Due.ToList());
    }

    public Task<Reminder> AddAsync(
        int userId,
        ReminderKind kind,
        string? title,
        int intervalHours,
        DateTime firstDueAt,
        CancellationToken cancellationToken = default)
    {
        AddCalls++;
        return _pendingAdd.Task;
    }

    public Task<bool> CompleteAsync(int id, DateTime expectedDueAt, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(false);

    public static ReminderItem Item(int id, DateTime dueAt, string title = "Tansiyon ölç") =>
        new(new Reminder { Id = id, UserId = 1, Kind = ReminderKind.Measurement, Title = title, IntervalHours = 24, NextDueAt = dueAt }, "Ali", true, 1);
}

/// <summary>Period report service whose answers are completed by the test, in any order.</summary>
internal sealed class ControlledPeriodReportService : IPeriodReportService
{
    private readonly List<(int UserId, TaskCompletionSource<PeriodReport> Answer)> _requests = [];

    public int BuildCalls => _requests.Count;

    public Task<PeriodReport> BuildAsync(int userId, DateTime firstDay, DateTime lastDay, CancellationToken cancellationToken = default)
    {
        var answer = new TaskCompletionSource<PeriodReport>();
        _requests.Add((userId, answer));
        return answer.Task;
    }

    /// <summary>Completes the oldest open request of <paramref name="user"/>.</summary>
    public void Respond(User user, int measurements)
    {
        var (_, answer) = _requests.First(r => r.UserId == user.Id && !r.Answer.Task.IsCompleted);
        answer.SetResult(new PeriodReport
        {
            User = user,
            From = new DateTime(2026, 1, 1),
            To = new DateTime(2026, 1, 7),
            GeneratedAt = new DateTime(2026, 1, 8),
            Records = Enumerable.Range(1, measurements)
                .Select(i => new HealthRecord { Id = i, UserId = user.Id, Date = new DateTime(2026, 1, i), Weight = 70, Pulse = 70 })
                .ToList(),
            Weight = null,
            Pulse = null,
            Goal = null,
        });
    }
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

using System.Globalization;
using SaglikTakip.Business.Insights;
using SaglikTakip.Business.Services;
using SaglikTakip.Business.Validation;
using SaglikTakip.Domain.Entities;

namespace SaglikTakip.Business.Tests;

public sealed class GoalEvaluatorTests
{
    private static readonly HealthGoal Limits = new() { UserId = 1, MinWeight = 60, MaxWeight = 80, MinPulse = 50, MaxPulse = 120 };

    [Theory]
    [InlineData(120, false)] // Exactly on the upper limit: still within range.
    [InlineData(121, true)]
    [InlineData(50, false)] // Exactly on the lower limit.
    [InlineData(49, true)]
    public void Check_PulseLimitsAreInclusive(int pulse, bool expectedAlert)
    {
        var alerts = GoalEvaluator.Check(Limits, Record(1, 70, pulse));

        Assert.Equal(expectedAlert, alerts.Count == 1);
    }

    [Theory]
    [InlineData(60.0, false)]
    [InlineData(59.9, true)]
    [InlineData(80.0, false)]
    [InlineData(80.1, true)]
    public void Check_WeightLimitsAreInclusive(double weight, bool expectedAlert)
    {
        var alerts = GoalEvaluator.Check(Limits, Record(1, weight, 70));

        Assert.Equal(expectedAlert, alerts.Count == 1);
    }

    [Fact]
    public void Check_ReportsSideLimitAndMetric()
    {
        var alerts = GoalEvaluator.Check(Limits, Record(1, 85, 40));

        Assert.Equal(
            [
                new ThresholdAlert(1, Day(1), HealthMetric.Weight, 85, 80, LimitSide.AboveMaximum),
                new ThresholdAlert(1, Day(1), HealthMetric.Pulse, 40, 50, LimitSide.BelowMinimum),
            ],
            alerts);
    }

    [Fact]
    public void Check_UnsetLimits_NeverAlert()
    {
        Assert.Empty(GoalEvaluator.Check(new HealthGoal { TargetWeight = 70 }, Record(1, 200, 250)));
        Assert.Empty(GoalEvaluator.Check(new HealthGoal { MaxPulse = 100 }, Record(1, 1, 100)));
    }

    [Theory]
    [InlineData(90, 80, 90, 0)]
    [InlineData(90, 80, 85, 50)]
    [InlineData(90, 80, 80, 100)]
    [InlineData(90, 80, 78, 100)] // Past the target: clamped.
    [InlineData(90, 80, 95, 0)] // Moving away from the target: clamped.
    [InlineData(60, 70, 65, 50)] // Gaining weight works the same way.
    [InlineData(70, 70, 70, 100)]
    [InlineData(70, 70, 71, 0)]
    public void Progress_FromStartToTarget(double start, double target, double current, double expected)
    {
        var goal = new HealthGoal { StartWeight = start, TargetWeight = target };

        Assert.Equal(expected, GoalEvaluator.Progress(goal, current)!.Value, 9);
    }

    [Fact]
    public void Progress_MissingStartTargetOrMeasurement_IsNull()
    {
        Assert.Null(GoalEvaluator.Progress(new HealthGoal { TargetWeight = 70 }, 75));
        Assert.Null(GoalEvaluator.Progress(new HealthGoal { StartWeight = 80 }, 75));
        Assert.Null(GoalEvaluator.Progress(new HealthGoal { StartWeight = 80, TargetWeight = 70 }, null));
    }

    [Fact]
    public void Evaluate_UsesTheLatestEntryAsCurrentWeight_AndListsAlertsOldestFirst()
    {
        var goal = new HealthGoal { StartWeight = 90, TargetWeight = 80, MaxPulse = 100 };
        HealthRecord[] records =
        [
            Record(3, 84, 110, Day(2)),
            Record(1, 88, 120, Day(1)),
            Record(2, 86, 90, Day(2)), // Same day, entered before record 3.
        ];

        var status = GoalEvaluator.Evaluate(goal, records);

        Assert.Equal(84, status.CurrentWeight);
        Assert.Equal(Day(2), status.CurrentWeightDate);
        Assert.Equal(60, status.ProgressPercent!.Value, 9);
        Assert.Equal([1, 3], status.Alerts.Select(a => a.RecordId));
    }

    [Fact]
    public void Evaluate_NoMeasurements_HasNoProgressAndNoAlerts()
    {
        var status = GoalEvaluator.Evaluate(new HealthGoal { StartWeight = 90, TargetWeight = 80 }, []);

        Assert.Null(status.CurrentWeight);
        Assert.Null(status.ProgressPercent);
        Assert.Empty(status.Alerts);
    }

    [Fact]
    public void Describe_IsTurkishAndNamesTheLimit()
    {
        var text = GoalEvaluator.Describe(
            new ThresholdAlert(1, Day(3), HealthMetric.Pulse, 130, 120, LimitSide.AboveMaximum),
            CultureInfo.GetCultureInfo("tr-TR"));

        Assert.Equal("3.06.2025: Nabız 130 atım/dk, üst sınır 120", text);
    }

    internal static DateTime Day(int day) => new(2025, 6, day, 0, 0, 0, DateTimeKind.Unspecified);

    private static HealthRecord Record(int id, double weight, int pulse, DateTime? date = null) =>
        new() { Id = id, UserId = 1, Date = date ?? Day(1), Weight = weight, Pulse = pulse };
}

public sealed class GoalInputValidatorTests
{
    [Theory]
    [InlineData(1, true)]
    [InlineData(200, true)]
    [InlineData(0.99, false)]
    [InlineData(200.01, false)]
    public void TargetWeight_Boundaries(double weight, bool expectedValid)
    {
        var result = Validate(new GoalInput((decimal)weight, null, null, null, null, null));

        Assert.Equal(expectedValid, result.IsValid);
        if (!expectedValid)
        {
            Assert.Equal("Hedef kilo 1 ile 200 arasında olmalıdır.", Assert.Single(result.Errors).ErrorMessage);
        }
    }

    [Theory]
    [InlineData(1, 250, true)]
    [InlineData(0, 100, false)]
    [InlineData(60, 251, false)]
    public void PulseLimits_Boundaries(int min, int max, bool expectedValid)
    {
        Assert.Equal(expectedValid, Validate(new GoalInput(null, null, null, null, min, max)).IsValid);
    }

    [Fact]
    public void EqualMinimumAndMaximum_AreAccepted()
    {
        Assert.True(Validate(new GoalInput(null, null, 70, 70, 80, 80)).IsValid);
    }

    [Theory]
    [InlineData(71, 70, 50, 60, "En düşük kilo en yüksek kilodan büyük olamaz.")]
    [InlineData(60, 70, 61, 60, "En düşük nabız en yüksek nabızdan büyük olamaz.")]
    public void ReversedLimits_AreRejected(int minWeight, int maxWeight, int minPulse, int maxPulse, string expected)
    {
        var result = Validate(new GoalInput(null, null, minWeight, maxWeight, minPulse, maxPulse));

        Assert.Equal(expected, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public void EmptyGoal_IsRejected()
    {
        var result = Validate(new GoalInput(null, null, null, null, null, null));

        Assert.Equal("En az bir hedef veya sınır girin.", Assert.Single(result.Errors).ErrorMessage);
    }

    private static FluentValidation.Results.ValidationResult Validate(GoalInput input) => new GoalInputValidator().Validate(input);
}

public sealed class GoalServiceTests
{
    private readonly InMemoryUserRepository _users = new();
    private readonly InMemoryHealthRecordRepository _records = new();
    private readonly InMemoryHealthGoalRepository _goals = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public GoalServiceTests()
    {
        _users.Users.Add(new User { Id = 1, Name = "Hedef", Age = 30, Gender = Genders.Female });
    }

    [Fact]
    public async Task Save_ThenSaveAgain_ReplacesTheSingleGoal()
    {
        var service = Service();

        await service.SaveAsync(1, new GoalInput(70, 80, null, null, 50, 120), Ct);
        await service.SaveAsync(1, new GoalInput(65, null, null, null, null, 110), Ct);

        var goal = Assert.Single(_goals.Goals);
        Assert.Equal(65, goal.TargetWeight);
        Assert.Null(goal.StartWeight);
        Assert.Null(goal.MinPulse);
        Assert.Equal(110, goal.MaxPulse);
    }

    [Fact]
    public async Task Save_InvalidInputOrUnknownUser_StoresNothing()
    {
        var service = Service();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SaveAsync(1, new GoalInput(null, null, null, null, 90, 80), Ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SaveAsync(5, new GoalInput(70, null, null, null, null, null), Ct));
        Assert.Empty(_goals.Goals);
    }

    [Fact]
    public async Task Status_UsesOnlyTheRange_AndIsNullWithoutGoal()
    {
        var service = Service();
        Assert.Null(await service.GetStatusAsync(1, GoalEvaluatorTests.Day(1), GoalEvaluatorTests.Day(30), Ct));

        await service.SaveAsync(1, new GoalInput(80, 90, null, null, null, 100), Ct);
        AddRecord(1, 88, 101);
        AddRecord(5, 85, 90);
        AddRecord(9, 82, 130); // Outside the range below.

        var status = await service.GetStatusAsync(1, GoalEvaluatorTests.Day(1), GoalEvaluatorTests.Day(5), Ct);

        Assert.NotNull(status);
        Assert.Equal(85, status.CurrentWeight);
        Assert.Equal(50, status.ProgressPercent!.Value, 9);
        Assert.Equal(GoalEvaluatorTests.Day(1), Assert.Single(status.Alerts).Date);
    }

    [Fact]
    public async Task Check_WithoutGoal_IsEmpty_AndDeleteRemovesTheGoal()
    {
        var service = Service();
        var record = new HealthRecord { Id = 1, UserId = 1, Date = GoalEvaluatorTests.Day(1), Weight = 150, Pulse = 200 };
        Assert.Empty(await service.CheckAsync(record, Ct));

        await service.SaveAsync(1, new GoalInput(null, null, null, 100, null, 150), Ct);
        Assert.Equal(2, (await service.CheckAsync(record, Ct)).Count);

        Assert.True(await service.DeleteAsync(1, Ct));
        Assert.False(await service.DeleteAsync(1, Ct));
        Assert.Null(await service.GetAsync(1, Ct));
    }

    private GoalService Service() => new(_users, _records, _goals, new GoalInputValidator());

    private void AddRecord(int day, double weight, int pulse) =>
        _records.Records.Add(new HealthRecord
        {
            Id = _records.Records.Count + 1,
            UserId = 1,
            Date = GoalEvaluatorTests.Day(day),
            Weight = weight,
            Pulse = pulse,
        });
}

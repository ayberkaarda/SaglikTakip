using SaglikTakip.Business.Insights;
using SaglikTakip.Business.Services;
using SaglikTakip.Domain.Entities;

namespace SaglikTakip.Business.Tests;

public sealed class TrendCalculatorTests
{
    [Fact]
    public void MovingAverage_WindowOfThree_IsNullUntilTheWindowIsFull()
    {
        var result = TrendCalculator.MovingAverage([1, 2, 3, 4, 5], 3);

        Assert.Equal([null, null, 2d, 3d, 4d], result);
    }

    [Fact]
    public void MovingAverage_WindowOfOne_ReturnsTheValues()
    {
        Assert.Equal([70d, 72d, 71d], TrendCalculator.MovingAverage([70, 72, 71], 1));
    }

    [Fact]
    public void MovingAverage_WindowEqualToCount_HasOnlyTheLastValue()
    {
        Assert.Equal([null, null, 2d], TrendCalculator.MovingAverage([1, 2, 3], 3));
    }

    [Fact]
    public void MovingAverage_WindowLargerThanCount_IsAllNull()
    {
        Assert.All(TrendCalculator.MovingAverage([1, 2], 3), Assert.Null);
    }

    [Fact]
    public void MovingAverage_EmptyInput_IsEmpty()
    {
        Assert.Empty(TrendCalculator.MovingAverage([], 7));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MovingAverage_WindowBelowOne_IsRejected(int window)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TrendCalculator.MovingAverage([1], window));
    }

    [Fact]
    public void DailyPoints_SameDayMeasurements_AreCombinedIntoTheirMean()
    {
        HealthRecord[] records =
        [
            Record(1, Day(3), 80, 90),
            Record(2, Day(1), 70, 60),
            Record(3, Day(1), 71, 80),
            Record(4, Day(2), 72, 70),
        ];

        var points = TrendCalculator.DailyPoints(records, HealthMetric.Pulse, 2);

        Assert.Equal([Day(1), Day(2), Day(3)], points.Select(p => p.Date));
        Assert.Equal([70d, 70d, 90d], points.Select(p => p.Value));
        Assert.Equal([2, 1, 1], points.Select(p => p.MeasurementCount));
        Assert.Equal([null, 70d, 80d], points.Select(p => p.MovingAverage));
    }

    [Fact]
    public void DailyPoints_IgnoreTheTimeOfDay()
    {
        HealthRecord[] records = [Record(1, Day(1).AddHours(8), 70, 60), Record(2, Day(1).AddHours(20), 72, 60)];

        var point = Assert.Single(TrendCalculator.DailyPoints(records, HealthMetric.Weight, 1));

        Assert.Equal(Day(1), point.Date);
        Assert.Equal(71d, point.Value);
        Assert.Equal(71d, point.MovingAverage);
    }

    [Fact]
    public void Summarize_UsesEverySingleMeasurement()
    {
        HealthRecord[] records =
        [
            Record(2, Day(1), 70, 60),
            Record(1, Day(1), 74, 100),
            Record(3, Day(2), 72, 80),
        ];

        var summary = TrendCalculator.Summarize(records, HealthMetric.Pulse);

        Assert.NotNull(summary);
        Assert.Equal(3, summary.Count);
        Assert.Equal(60d, summary.Minimum);
        Assert.Equal(100d, summary.Maximum);
        Assert.Equal(80d, summary.Average);
        Assert.Equal(100d, summary.First); // Same day: the earlier entry (lower id) comes first.
        Assert.Equal(80d, summary.Last);
    }

    [Fact]
    public void Summarize_SingleMeasurement_HasEqualMinMaxAndAverage()
    {
        var summary = TrendCalculator.Summarize([Record(1, Day(1), 68.5, 70)], HealthMetric.Weight);

        Assert.Equal(new MetricSummary(1, 68.5, 68.5, 68.5, 68.5, 68.5), summary);
    }

    [Fact]
    public void Summarize_NoMeasurement_IsNull()
    {
        Assert.Null(TrendCalculator.Summarize([], HealthMetric.Weight));
    }

    internal static DateTime Day(int day) => new(2025, 6, day, 0, 0, 0, DateTimeKind.Unspecified);

    internal static HealthRecord Record(int id, DateTime date, double weight, int pulse) =>
        new() { Id = id, UserId = 1, Date = date, Weight = weight, Pulse = pulse };
}

public sealed class TrendServiceTests
{
    private readonly InMemoryUserRepository _users = new();
    private readonly InMemoryHealthRecordRepository _records = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public TrendServiceTests()
    {
        _users.Users.Add(new User { Id = 1, Name = "Trend", Age = 30, Gender = Genders.Female });
        _users.Users.Add(new User { Id = 2, Name = "Diğer", Age = 40, Gender = Genders.Male });
    }

    [Fact]
    public async Task GetTrend_IncludesBothEndDaysAndOnlyTheSelectedUser()
    {
        Add(1, 4, 70);
        Add(1, 5, 71);
        Add(1, 7, 72);
        Add(1, 8, 73);
        Add(2, 6, 99);

        var trend = await Service().GetTrendAsync(1, HealthMetric.Weight, Day(5).AddHours(15), Day(7), 1, Ct);

        Assert.Equal(Day(5), trend.From);
        Assert.Equal([71d, 72d], trend.Points.Select(p => p.Value));
        Assert.Equal(2, trend.Summary?.Count);
    }

    [Fact]
    public async Task GetTrend_EmptyRange_HasNoPointsAndNoSummary()
    {
        Add(1, 1, 70);

        var trend = await Service().GetTrendAsync(1, HealthMetric.Pulse, Day(10), Day(20), 7, Ct);

        Assert.Empty(trend.Points);
        Assert.Null(trend.Summary);
    }

    [Fact]
    public async Task GetTrend_SingleDayRange_IsAccepted()
    {
        Add(1, 3, 70);
        Add(1, 3, 72);

        var trend = await Service().GetTrendAsync(1, HealthMetric.Weight, Day(3), Day(3), 1, Ct);

        Assert.Equal(71d, Assert.Single(trend.Points).Value);
        Assert.Equal(2, trend.Summary?.Count);
    }

    [Fact]
    public async Task GetTrend_StartAfterEnd_IsRejected()
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service().GetTrendAsync(1, HealthMetric.Weight, Day(2), Day(1), 1, Ct));

        Assert.Equal("Başlangıç tarihi bitiş tarihinden sonra olamaz.", error.Message);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(90, true)]
    [InlineData(91, false)]
    public async Task GetTrend_WindowBoundaries(int window, bool expectedValid)
    {
        var call = () => Service().GetTrendAsync(1, HealthMetric.Weight, Day(1), Day(2), window, Ct);

        if (expectedValid)
        {
            Assert.Equal(window, (await call()).MovingAverageWindow);
        }
        else
        {
            var error = await Assert.ThrowsAsync<BusinessRuleException>(call);
            Assert.Equal("Hareketli ortalama gün sayısı 1 ile 90 arasında olmalıdır.", error.Message);
        }
    }

    [Fact]
    public async Task GetTrend_UnknownUser_IsRejected()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service().GetTrendAsync(9, HealthMetric.Weight, Day(1), Day(2), 1, Ct));
    }

    private static DateTime Day(int day) => TrendCalculatorTests.Day(day);

    private TrendService Service() => new(_users, _records);

    private void Add(int userId, int day, double weight) =>
        _records.Records.Add(new HealthRecord
        {
            Id = _records.Records.Count + 1,
            UserId = userId,
            Date = Day(day),
            Weight = weight,
            Pulse = 70,
        });
}

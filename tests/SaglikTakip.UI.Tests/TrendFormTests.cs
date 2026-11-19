using System.Drawing;
using SaglikTakip.Business.Insights;
using SaglikTakip.Business.Services;
using SaglikTakip.Domain.Entities;
using SaglikTakip.UI.Infrastructure;
using SaglikTakip.UI.Tests.Support;
using static SaglikTakip.UI.Tests.Support.StaThread;

namespace SaglikTakip.UI.Tests;

public sealed class TrendFormTests
{
    [Fact]
    public void SelectionChanged_OutOfOrderResponses_KeepLatestSelection() => Run(() =>
    {
        var trends = new ControlledTrendService();
        using var form = new TrendForm(new PendingUserService(), trends, new ControlledGoalService { AnswerImmediately = true });
        var picker = Find<ComboBox>(form, "userPicker");
        var chart = Find<TrendChart>(form, "trendChart");
        picker.Items.Add(new User { Id = 1, Name = "Ali", Age = 40, Gender = Genders.Male });
        picker.Items.Add(new User { Id = 2, Name = "Ayşe", Age = 30, Gender = Genders.Female });

        picker.SelectedIndex = 0; // Raises a load for user 1.
        picker.SelectedIndex = 1; // Raises a load for user 2; user 1's chart must never appear.
        Assert.Equal(2, trends.Calls);
        Assert.Empty(chart.Points);

        trends.Respond(2, 80, 81);
        trends.Respond(1, 60);

        Assert.Equal([80d, 81d], chart.Points.Select(p => p.Value));
    });

    [Fact]
    public void Summary_WithSparseDays_LabelsObservedDayWindow()
    {
        var trend = new MetricTrend
        {
            Metric = HealthMetric.Weight,
            From = new DateTime(2026, 6, 1),
            To = new DateTime(2026, 6, 30),
            MovingAverageWindow = 2,
            Points = [new(new DateTime(2026, 6, 1), 60, 1, 60), new(new DateTime(2026, 6, 30), 80, 1, 70)],
            Summary = new MetricSummary(2, 60, 80, 70, 60, 80),
        };

        var text = TrendForm.DescribeSummary(trend, "kg");

        Assert.Contains("Son 2 ölçüm gününün ortalaması: 70 kg", text, StringComparison.Ordinal);
        Assert.DoesNotContain("günlük ortalama", text, StringComparison.Ordinal);
    }

    [Fact]
    public void NoUserSelected_ShowsHintWithoutCallingTheService() => Run(() =>
    {
        var trends = new ControlledTrendService();
        using var form = new TrendForm(new PendingUserService(), trends, new ControlledGoalService { AnswerImmediately = true });

        var load = form.ShowTrendAsync();

        Assert.True(load.IsCompletedSuccessfully);
        Assert.Equal(0, trends.Calls);
        Assert.Equal(TrendForm.NoUser, Find<TrendChart>(form, "trendChart").EmptyText);
    });

    [Fact]
    public void Loading_IsShownUntilTheAnswerArrives() => Run(() =>
    {
        var trends = new ControlledTrendService();
        using var form = new TrendForm(new PendingUserService(), trends, new ControlledGoalService { AnswerImmediately = true });
        var picker = Find<ComboBox>(form, "userPicker");
        var chart = Find<TrendChart>(form, "trendChart");
        picker.Items.Add(new User { Id = 1, Name = "Ali", Age = 40, Gender = Genders.Male });

        picker.SelectedIndex = 0;
        Assert.True(chart.IsLoading);
        Assert.Equal(TrendForm.Loading, chart.EmptyText);

        trends.Respond(1);
        Assert.False(chart.IsLoading);
        Assert.Equal(TrendForm.NoData, chart.EmptyText); // An empty range explains itself.
    });

    [Fact]
    public void Chart_RendersOffscreenWithAndWithoutData() => Run(() =>
    {
        using var chart = new TrendChart { Size = new Size(400, 240) };
        using var bitmap = new Bitmap(400, 240);
        using var graphics = Graphics.FromImage(bitmap);

        chart.Render(graphics, new Rectangle(0, 0, 400, 240));
        chart.SetData(
            [new(new DateTime(2026, 1, 1), 70, 1, null), new(new DateTime(2026, 1, 3), 72, 2, 71)],
            lowerLimit: 60,
            upperLimit: 75);
        chart.Render(graphics, new Rectangle(0, 0, 400, 240));
        chart.SetData([new(new DateTime(2026, 1, 1), 70, 1, 70)]);
        chart.Render(graphics, new Rectangle(0, 0, 400, 240));
    });
}

public sealed class ChartScaleTests
{
    private static readonly RectangleF Plot = new(10, 20, 100, 50);

    [Fact]
    public void MapY_MinimumAtBottomMaximumAtTop()
    {
        Assert.Equal(70f, ChartScale.MapY(0, 0, 10, Plot));
        Assert.Equal(20f, ChartScale.MapY(10, 0, 10, Plot));
        Assert.Equal(45f, ChartScale.MapY(5, 0, 10, Plot));
    }

    [Fact]
    public void MapX_FollowsTheCalendarAndCentersASingleDay()
    {
        var first = new DateTime(2026, 1, 1);

        Assert.Equal(10f, ChartScale.MapX(first, first, first.AddDays(4), Plot));
        Assert.Equal(35f, ChartScale.MapX(first.AddDays(1), first, first.AddDays(4), Plot));
        Assert.Equal(110f, ChartScale.MapX(first.AddDays(4), first, first.AddDays(4), Plot));
        Assert.Equal(60f, ChartScale.MapX(first, first, first, Plot));
    }

    [Fact]
    public void NiceTicks_UseRoundStepsThatCoverTheRange()
    {
        Assert.Equal([50d, 60d, 70d, 80d, 90d], ChartScale.NiceTicks(59, 81, 5));
        Assert.Equal([68d, 70d, 72d, 74d, 76d, 78d], ChartScale.NiceTicks(68.9, 77.6, 5));

        var ticks = ChartScale.NiceTicks(70, 70, 5);
        Assert.True(ticks[0] <= 69 && ticks[^1] >= 71);
        Assert.All(ticks.Zip(ticks.Skip(1)), pair => Assert.True(pair.Second > pair.First));
    }

    [Fact]
    public void DateTicks_IncludeBothEndsWithoutCrowding()
    {
        var first = new DateTime(2026, 10, 21);

        var month = ChartScale.DateTicks(first, first.AddDays(29), 8);
        Assert.Equal(first, month[0]);
        Assert.Equal(first.AddDays(29), month[^1]);
        Assert.True(month.Count <= 8);
        Assert.All(month.Zip(month.Skip(1)), pair => Assert.True((pair.Second - pair.First).TotalDays >= 2));

        Assert.Equal([first], ChartScale.DateTicks(first, first, 5));
    }

    [Fact]
    public void ValueRange_AddsHeadroomAndWidensASingleValue()
    {
        Assert.Equal((69d, 71d), ChartScale.ValueRange([70, 70]));
        var (min, max) = ChartScale.ValueRange([60, 80]);
        Assert.Equal(59d, min, 9);
        Assert.Equal(81d, max, 9);
        Assert.Equal((0d, 1d), ChartScale.ValueRange([]));
    }
}

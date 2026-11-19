using System.Text;
using Microsoft.Extensions.Time.Testing;
using SaglikTakip.Business.Insights;
using SaglikTakip.Business.Reporting;
using SaglikTakip.Business.Services;
using SaglikTakip.Domain.Entities;

namespace SaglikTakip.Business.Tests;

public sealed class CsvReportWriterTests
{
    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+90 555", "'+90 555")]
    [InlineData("-5", "'-5")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("\tx", "'\tx")]
    [InlineData("Not = iyi", "Not = iyi")] // Only a leading character is dangerous.
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Cell_NeutralisesFormulaStarts(string? value, string expected)
    {
        Assert.Equal(expected, CsvReportWriter.Cell(value));
    }

    [Theory]
    [InlineData("a;b", "\"a;b\"")]
    [InlineData("\"tırnak\"", "\"\"\"tırnak\"\"\"")]
    [InlineData("iki\nsatır", "\"iki\nsatır\"")]
    [InlineData("=A1;B1", "\"'=A1;B1\"")]
    public void Cell_QuotesSeparatorsQuotesAndLineBreaks(string value, string expected)
    {
        Assert.Equal(expected, CsvReportWriter.Cell(value));
    }

    [Fact]
    public void Write_HasSectionsTurkishNumbersAndFlaggedRows()
    {
        var text = WriteCsv(ReportSamples.WithGoal());

        Assert.Contains("Kullanıcı;Ayşe", text, StringComparison.Ordinal);
        Assert.Contains("Başlangıç;2025-06-01", text, StringComparison.Ordinal);
        Assert.Contains("Kilo (kg);2;70,5;72;71,25;70,5;72", text, StringComparison.Ordinal);
        Assert.Contains("2025-06-01;70,5;72;;\"sabah; aç karnına\"", text, StringComparison.Ordinal);
        Assert.Contains("2025-06-02;72;130;Nabız yüksek;\"'=HYPERLINK(\"\"x\"\")\"", text, StringComparison.Ordinal);
        Assert.Contains("Hedefe ilerleme (%);", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_EmptyRange_StillHasHeaderAndSummary()
    {
        var text = WriteCsv(ReportSamples.Empty());

        Assert.Contains("Kilo (kg);0", text, StringComparison.Ordinal);
        Assert.EndsWith("Tarih;Kilo (kg);Nabız (atım/dk);Hedef dışı;Notlar" + Environment.NewLine, text, StringComparison.Ordinal);
    }

    private static string WriteCsv(PeriodReport report)
    {
        using var writer = new StringWriter();
        CsvReportWriter.Write(report, writer);
        return writer.ToString();
    }
}

public sealed class HtmlReportWriterTests
{
    [Fact]
    public void Write_EncodesUserTextAndHasNoScripts()
    {
        using var writer = new StringWriter();
        var report = ReportSamples.WithGoal(name: "<script>alert(1)</script>");

        HtmlReportWriter.Write(report, writer);
        var html = writer.ToString();

        Assert.StartsWith("<!DOCTYPE html>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html, StringComparison.Ordinal);
        Assert.Contains("default-src 'none'", html, StringComparison.Ordinal);
        Assert.Contains("<tr class=\"alert\">", html, StringComparison.Ordinal);
        Assert.Contains("<polyline", html, StringComparison.Ordinal);
        Assert.Contains("@page", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_EmptyRange_SaysSo()
    {
        using var writer = new StringWriter();

        HtmlReportWriter.Write(ReportSamples.Empty(), writer);

        Assert.Contains("Bu dönemde ölçüm yok.", writer.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("<polyline", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Sparkline_UsesInvariantNumbers()
    {
        var svg = HtmlReportWriter.Sparkline([(new DateTime(2025, 6, 1), 70.5), (new DateTime(2025, 6, 2), 71.25)], null);

        Assert.Contains("points=\"12,148 628,12\"", svg, StringComparison.Ordinal);
    }
}

public sealed class ReportExporterTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("sagliktakip-export-").FullName;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Csv_IsUtf8WithBom_AndNoTemporaryFileRemains()
    {
        var path = Path.Combine(_directory, "rapor.csv");

        await ReportExporter.ExportAsync(ReportSamples.WithGoal(), ReportFormat.Csv, path, Ct);

        var bytes = await File.ReadAllBytesAsync(path, Ct);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes.Take(3));
        Assert.Contains("Kullanıcı;Ayşe", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
        Assert.Equal([path], Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task Html_HasNoBom_AndReplacesAnExistingFile()
    {
        var path = Path.Combine(_directory, "rapor.html");
        await File.WriteAllTextAsync(path, "eski", Ct);

        await ReportExporter.ExportAsync(ReportSamples.Empty(), ReportFormat.Html, path, Ct);

        var bytes = await File.ReadAllBytesAsync(path, Ct);
        Assert.Equal((byte)'<', bytes[0]);
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task FailureWhileWriting_KeepsTheOriginalAndRemovesTheTemporaryFile()
    {
        var path = Path.Combine(_directory, "rapor.csv");
        await File.WriteAllTextAsync(path, "önceki rapor", Ct);

        await Assert.ThrowsAsync<InvalidOperationException>(() => ReportExporter.WriteAtomicallyAsync(
            path,
            CsvReportWriter.Encoding,
            writer =>
            {
                writer.Write("yarım");
                throw new InvalidOperationException("disk dolu");
            },
            Ct));

        Assert.Equal("önceki rapor", await File.ReadAllTextAsync(path, Ct));
        Assert.Equal([path], Directory.GetFiles(_directory));
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}

public sealed class PeriodReportServiceTests
{
    private readonly InMemoryUserRepository _users = new();
    private readonly InMemoryHealthRecordRepository _records = new();
    private readonly InMemoryHealthGoalRepository _goals = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2025, 7, 1, 10, 0, 0, TimeSpan.Zero));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PeriodReportServiceTests()
    {
        _users.Users.Add(new User { Id = 1, Name = "Ayşe", Age = 30, Gender = Genders.Female });
    }

    [Fact]
    public async Task Build_WeekRange_IncludesBothEnds_AndSummarisesSameDayMeasurements()
    {
        Add(new DateTime(2025, 6, 23), 71, 60); // Day before the range.
        Add(new DateTime(2025, 6, 24), 70, 70);
        Add(new DateTime(2025, 6, 30), 69, 80);
        Add(new DateTime(2025, 6, 30), 68, 90);
        var week = DayRanges.LastDays(new DateTime(2025, 6, 30), 7);

        var report = await Service().BuildAsync(1, week.First, week.Last, Ct);

        Assert.Equal(new DateTime(2025, 6, 24), report.From);
        Assert.Equal(7, report.Days);
        Assert.Equal(3, report.Records.Count);
        Assert.Equal(new MetricSummary(3, 70, 90, 80, 70, 90), report.Pulse);
        Assert.Null(report.Goal);
    }

    [Fact]
    public async Task Build_EmptyRangeWithGoal_HasNoSummaryAndNoProgress()
    {
        _goals.Goals.Add(new HealthGoal { Id = 1, UserId = 1, StartWeight = 80, TargetWeight = 70 });

        var report = await Service().BuildAsync(1, new DateTime(2025, 1, 1), new DateTime(2025, 1, 31), Ct);

        Assert.Empty(report.Records);
        Assert.Null(report.Weight);
        Assert.NotNull(report.Goal);
        Assert.Null(report.Goal.ProgressPercent);
    }

    [Fact]
    public async Task Build_ReversedRangeOrUnknownUser_IsRejected()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service().BuildAsync(1, new DateTime(2025, 2, 1), new DateTime(2025, 1, 31), Ct));
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service().BuildAsync(7, new DateTime(2025, 1, 1), new DateTime(2025, 1, 31), Ct));
    }

    [Fact]
    public void DayRanges_WeekAndMonths()
    {
        var today = new DateTime(2025, 3, 15);

        Assert.Equal((new DateTime(2025, 3, 9), today), DayRanges.LastDays(today, 7));
        Assert.Equal((today, today), DayRanges.LastDays(today, 1));
        Assert.Equal((new DateTime(2025, 3, 1), today), DayRanges.MonthToDate(today));
        Assert.Equal((new DateTime(2025, 2, 1), new DateTime(2025, 2, 28)), DayRanges.PreviousMonth(today));
        Assert.Equal((new DateTime(2024, 12, 1), new DateTime(2024, 12, 31)), DayRanges.PreviousMonth(new DateTime(2025, 1, 10)));
    }

    private PeriodReportService Service() => new(_users, _records, _goals, _time);

    private void Add(DateTime date, double weight, int pulse) =>
        _records.Records.Add(new HealthRecord { Id = _records.Records.Count + 1, UserId = 1, Date = date, Weight = weight, Pulse = pulse });
}

internal static class ReportSamples
{
    public static PeriodReport WithGoal(string name = "Ayşe")
    {
        HealthRecord[] records =
        [
            new() { Id = 1, UserId = 1, Date = new DateTime(2025, 6, 1), Weight = 70.5, Pulse = 72, Notes = "sabah; aç karnına" },
            new() { Id = 2, UserId = 1, Date = new DateTime(2025, 6, 2), Weight = 72, Pulse = 130, Notes = "=HYPERLINK(\"x\")" },
        ];
        var goal = new HealthGoal { UserId = 1, TargetWeight = 68, StartWeight = 74, MaxPulse = 120 };
        return new PeriodReport
        {
            User = new User { Id = 1, Name = name, Age = 30, Gender = Genders.Female },
            From = new DateTime(2025, 6, 1),
            To = new DateTime(2025, 6, 7),
            GeneratedAt = new DateTime(2025, 6, 8, 9, 30, 0),
            Records = records,
            Weight = TrendCalculator.Summarize(records, HealthMetric.Weight),
            Pulse = TrendCalculator.Summarize(records, HealthMetric.Pulse),
            Goal = GoalEvaluator.Evaluate(goal, records),
        };
    }

    public static PeriodReport Empty() => new()
    {
        User = new User { Id = 1, Name = "Ayşe", Age = 30, Gender = Genders.Female },
        From = new DateTime(2025, 1, 1),
        To = new DateTime(2025, 1, 31),
        GeneratedAt = new DateTime(2025, 2, 1, 8, 0, 0),
        Records = [],
        Weight = null,
        Pulse = null,
        Goal = null,
    };
}

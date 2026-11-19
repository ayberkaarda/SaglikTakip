using SaglikTakip.Business.Reporting;
using SaglikTakip.Domain.Entities;
using SaglikTakip.UI.Tests.Support;
using static SaglikTakip.UI.Tests.Support.StaThread;

namespace SaglikTakip.UI.Tests;

public sealed class DonemRaporuFormTests
{
    private static readonly User Ali = new() { Id = 1, Name = "Ali", Age = 40, Gender = Genders.Male };
    private static readonly User Ayse = new() { Id = 2, Name = "Ayşe", Age = 30, Gender = Genders.Female };

    [Fact]
    public void Preview_OutOfOrderResponses_KeepLatestSelection() => Run(() =>
    {
        var reports = new ControlledPeriodReportService();
        using var form = NewForm(reports);
        var picker = Find<ComboBox>(form, "userPicker");
        var preview = Find<TextBox>(form, "previewText");

        picker.SelectedIndex = 0;
        picker.SelectedIndex = 1;
        Assert.Equal(DonemRaporuForm.Loading, preview.Text);

        reports.Respond(Ayse, 2);
        reports.Respond(Ali, 5);

        Assert.Contains("Ayşe", preview.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Ali", preview.Text, StringComparison.Ordinal);
    });

    [Fact]
    public void Export_WhilePending_DoesNotBuildTwice() => Run(() =>
    {
        var reports = new ControlledPeriodReportService();
        using var form = NewForm(reports);
        Find<ComboBox>(form, "userPicker").SelectedIndex = 0;
        var previewCalls = reports.BuildCalls;

        var first = form.ExportAsync(ReportFormat.Csv);
        var second = form.ExportAsync(ReportFormat.Html);

        Assert.Equal(previewCalls + 1, reports.BuildCalls);
        Assert.False(first.IsCompleted);
        Assert.True(second.IsCompletedSuccessfully);
        Assert.False(second.Result);
        Assert.False(Find<Button>(form, "btnCsv").Enabled);
        Assert.False(Find<ComboBox>(form, "userPicker").Enabled);
    });

    [Fact]
    public void Export_WritesTheReportOfTheSelectionAtClickTime() => Run(() =>
    {
        var directory = Directory.CreateTempSubdirectory("sagliktakip-ui-export-").FullName;
        try
        {
            var reports = new ControlledPeriodReportService();
            using var form = NewForm(reports);
            var target = Path.Combine(directory, "rapor.html");
            string? suggested = null;
            form.ChoosePath = (_, name) =>
            {
                suggested = name;
                return target;
            };
            form.OfferToOpen = _ => false;
            Find<ComboBox>(form, "userPicker").SelectedIndex = 1;
            reports.Respond(Ayse, 1); // Preview.

            var export = form.ExportAsync(ReportFormat.Html);
            reports.Respond(Ayse, 3);
            SpinUntil(() => export.IsCompleted);

            Assert.True(export.Result);
            Assert.Equal("saglik-raporu-2026-01-01_2026-01-07.html", suggested);
            var html = File.ReadAllText(target);
            Assert.Contains("Ayşe", html, StringComparison.Ordinal);
            Assert.Contains("03.01.2026", html, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    });

    private static DonemRaporuForm NewForm(ControlledPeriodReportService reports)
    {
        var form = new DonemRaporuForm(new PendingUserService(), reports);
        var picker = Find<ComboBox>(form, "userPicker");
        picker.Items.Add(Ali);
        picker.Items.Add(Ayse);
        return form;
    }

    /// <summary>The file write completes on a thread pool thread; waits briefly for the continuation.</summary>
    private static void SpinUntil(Func<bool> condition)
    {
        if (!SpinWait.SpinUntil(condition, TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException("The export did not finish in time.");
        }
    }
}

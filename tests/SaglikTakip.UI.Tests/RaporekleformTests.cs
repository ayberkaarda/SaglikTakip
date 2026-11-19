using System.Globalization;
using SaglikTakip.Domain.Entities;
using SaglikTakip.UI.Tests.Support;
using static SaglikTakip.UI.Tests.Support.StaThread;

namespace SaglikTakip.UI.Tests;

public sealed class RaporekleformTests
{
    [Fact]
    public void SelectionChanged_OutOfOrderResponses_KeepLatestSelection() => Run(() =>
    {
        var reports = new ControlledReportService();
        using var form = new Raporekleform(new PendingUserService(), reports);
        var picker = Find<ComboBox>(form, "comboBox1");
        var weight = Find<Label>(form, "lblKilo");
        var first = new User { Id = 1, Name = "Ali", Age = 40, Gender = Genders.Male };
        var second = new User { Id = 2, Name = "Ayşe", Age = 30, Gender = Genders.Female };
        picker.Items.Add(first);
        picker.Items.Add(second);

        picker.SelectedIndex = 0;
        var firstLoad = form.ShowSelectedUserAsync();
        reports.Respond(first, 60);
        Assert.Equal(60.ToString(CultureInfo.CurrentCulture), weight.Text);

        // A new selection clears the previous user's report right away.
        picker.SelectedIndex = 0;
        var staleLoad = form.ShowSelectedUserAsync();
        picker.SelectedIndex = 1;
        var latestLoad = form.ShowSelectedUserAsync();
        Assert.NotEqual(60.ToString(CultureInfo.CurrentCulture), weight.Text);

        // The latest response arrives first, the stale one afterwards.
        reports.Respond(second, 80);
        reports.Respond(first, 61);

        Assert.True(firstLoad.IsCompletedSuccessfully);
        Assert.True(staleLoad.IsCompletedSuccessfully);
        Assert.True(latestLoad.IsCompletedSuccessfully);
        Assert.Equal(80.ToString(CultureInfo.CurrentCulture), weight.Text);
    });
}

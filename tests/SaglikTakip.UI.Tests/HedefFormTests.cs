using SaglikTakip.Domain.Entities;
using SaglikTakip.UI.Infrastructure;
using SaglikTakip.UI.Tests.Support;
using static SaglikTakip.UI.Tests.Support.StaThread;

namespace SaglikTakip.UI.Tests;

public sealed class HedefFormTests
{
    private static readonly User Ali = new() { Id = 1, Name = "Ali", Age = 40, Gender = Genders.Male };
    private static readonly User Ayse = new() { Id = 2, Name = "Ayşe", Age = 30, Gender = Genders.Female };

    [Fact]
    public void LateGoalOfThePreviousUser_IsNeitherShownNorSavedForTheCurrentUser() => Run(() =>
    {
        var goals = new ControlledGoalService();
        using var form = new HedefForm(new PendingUserService(), goals);
        var picker = Find<ComboBox>(form, "userPicker");
        var save = Find<Button>(form, "btnKaydet");
        var target = Find<OptionalNumberInput>(form, "targetWeight");
        picker.Items.Add(Ali);
        picker.Items.Add(Ayse);

        picker.SelectedIndex = 0;
        picker.SelectedIndex = 1;
        Assert.False(save.Enabled); // Nothing can be saved until the current user's goal is on screen.

        goals.Respond(Ayse.Id, 55);
        goals.Respond(Ali.Id, 90); // Arrives last but belongs to the previous selection.

        Assert.Equal(55m, target.Value);
        Assert.True(save.Enabled);

        _ = form.SaveAsync();
        var (userId, input) = Assert.Single(goals.Saved);
        Assert.Equal(Ayse.Id, userId);
        Assert.Equal(55m, input.TargetWeight);
    });

    [Fact]
    public void Save_WhilePending_DoesNotWriteTwice() => Run(() =>
    {
        var goals = new ControlledGoalService();
        using var form = new HedefForm(new PendingUserService(), goals);
        var picker = Find<ComboBox>(form, "userPicker");
        picker.Items.Add(Ali);
        picker.SelectedIndex = 0;
        goals.Respond(Ali.Id, 80);

        var first = form.SaveAsync();
        var second = form.SaveAsync();

        Assert.Equal(1, goals.SaveCalls);
        Assert.False(first.IsCompleted);
        Assert.True(second.IsCompleted);
        Assert.False(Find<Button>(form, "btnKaydet").Enabled);
        Assert.False(picker.Enabled);
    });
}

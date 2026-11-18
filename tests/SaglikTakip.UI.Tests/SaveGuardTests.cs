using SaglikTakip.Domain.Entities;
using SaglikTakip.UI.Tests.Support;
using static SaglikTakip.UI.Tests.Support.StaThread;

namespace SaglikTakip.UI.Tests;

public sealed class KullaniciEkleFormTests
{
    [Fact]
    public void Save_WhilePending_DoesNotInsertTwice() => Run(() =>
    {
        var users = new PendingUserService();
        using var form = new KullaniciEkleForm(users);
        Find<TextBox>(form, "textBoxad").Text = "Ayşe";
        Find<TextBox>(form, "textBoxyas").Text = "30";

        var first = form.SaveAsync();
        var second = form.SaveAsync();

        Assert.Equal(1, users.AddCalls);
        Assert.False(first.IsCompleted);
        Assert.True(second.IsCompleted);
        Assert.False(Find<Button>(form, "btnKaydet").Enabled);
    });
}

public sealed class EgzersizKayitFormTests
{
    [Fact]
    public void Save_WhilePending_DoesNotInsertTwice() => Run(() =>
    {
        var exercises = new PendingExerciseService();
        using var form = new EgzersizKayitForm(new PendingUserService(), exercises);
        var picker = Find<ComboBox>(form, "comboBox1");
        picker.Items.Add(new User { Id = 1, Name = "Ayşe", Age = 30, Gender = Genders.Female });
        picker.SelectedIndex = 0;
        Find<TextBox>(form, "textBox1").Text = "Koşu";

        var first = form.SaveAsync();
        var second = form.SaveAsync();

        Assert.Equal(1, exercises.AddCalls);
        Assert.False(first.IsCompleted);
        Assert.True(second.IsCompleted);
        Assert.False(Find<Button>(form, "btnKaydet").Enabled);
    });
}

public sealed class SaglikKayitFormTests
{
    [Fact]
    public void Save_WhilePending_DoesNotInsertTwice() => Run(() =>
    {
        var records = new PendingHealthRecordService();
        using var form = new SaglikKayitForm(new PendingUserService(), records);
        var picker = Find<ComboBox>(form, "comboBox1");
        picker.Items.Add(new User { Id = 1, Name = "Ayşe", Age = 30, Gender = Genders.Female });
        picker.SelectedIndex = 0;

        var first = form.SaveAsync();
        var second = form.SaveAsync();

        Assert.Equal(1, records.AddCalls);
        Assert.False(first.IsCompleted);
        Assert.True(second.IsCompleted);
        Assert.False(Find<Button>(form, "btnKaydet").Enabled);
    });
}

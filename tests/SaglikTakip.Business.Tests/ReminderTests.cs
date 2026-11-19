using Microsoft.Extensions.Time.Testing;
using SaglikTakip.Business.Insights;
using SaglikTakip.Business.Services;
using SaglikTakip.Business.Validation;
using SaglikTakip.Domain.Entities;

namespace SaglikTakip.Business.Tests;

public sealed class ReminderScheduleTests
{
    private static readonly DateTime Due = new(2025, 6, 15, 9, 0, 0, DateTimeKind.Unspecified);

    [Fact]
    public void IsDue_ExactlyAtTheDueTime_IsDue()
    {
        Assert.True(ReminderSchedule.IsDue(Due, Due));
        Assert.False(ReminderSchedule.IsDue(Due, Due.AddTicks(-1)));
    }

    [Theory]
    [InlineData(-1, 0)] // One minute before: not due.
    [InlineData(0, 1)] // Exactly due.
    [InlineData(23 * 60 + 59, 1)]
    [InlineData(24 * 60, 2)] // The next daily occurrence has come due as well.
    [InlineData(3 * 24 * 60 + 1, 4)]
    public void DueOccurrences_CountsMissedDailyOccurrences(int minutesAfterDue, int expected)
    {
        Assert.Equal(expected, ReminderSchedule.DueOccurrences(Due, 24, Due.AddMinutes(minutesAfterDue)));
    }

    [Theory]
    [InlineData(0, 24)] // Done exactly at the due time: next one a full interval later.
    [InlineData(60, 24)]
    [InlineData(24 * 60, 48)] // A missed occurrence is skipped, not replayed.
    [InlineData(-30, 0)] // Not due yet: unchanged.
    public void NextAfter_IsTheFirstOccurrenceStrictlyAfterNow(int minutesAfterDue, int expectedHoursAfterDue)
    {
        Assert.Equal(Due.AddHours(expectedHoursAfterDue), ReminderSchedule.NextAfter(Due, 24, Due.AddMinutes(minutesAfterDue)));
    }

    [Fact]
    public void ToMinute_DropsSecondsAndTicks()
    {
        Assert.Equal(Due, ReminderSchedule.ToMinute(Due.AddSeconds(59).AddTicks(9)));
    }
}

public sealed class ReminderInputValidatorTests
{
    private static readonly DateTime Now = new(2025, 6, 15, 12, 30, 40, DateTimeKind.Unspecified);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Now, TimeSpan.Zero));

    public ReminderInputValidatorTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(8760, true)]
    [InlineData(8761, false)]
    public void IntervalBoundaries(int hours, bool expectedValid)
    {
        var result = Validate(intervalHours: hours);

        Assert.Equal(expectedValid, result.IsValid);
        if (!expectedValid)
        {
            Assert.Equal("Tekrar aralığı (saat) 1 ile 8760 arasında olmalıdır.", Assert.Single(result.Errors).ErrorMessage);
        }
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void TitleLengthBoundary(int length, bool expectedValid)
    {
        Assert.Equal(expectedValid, Validate(title: new string('t', length)).IsValid);
    }

    [Fact]
    public void BlankTitleOrUnknownKind_AreRejected()
    {
        Assert.Equal("Başlık alanı boş bırakılamaz.", Assert.Single(Validate(title: "  ").Errors).ErrorMessage);
        Assert.Equal("Lütfen hatırlatıcı türünü seçin.", Assert.Single(Validate(kind: (ReminderKind)7).Errors).ErrorMessage);
    }

    [Fact]
    public void FirstDueTime_CurrentMinuteIsAccepted_PreviousMinuteIsNot()
    {
        Assert.True(Validate(firstDueAt: Now.AddSeconds(-40)).IsValid); // 12:30:00, same minute as now.
        var result = Validate(firstDueAt: Now.AddSeconds(-41)); // 12:29:59 -> 12:29.

        Assert.Equal("İlk hatırlatma zamanı geçmişte olamaz.", Assert.Single(result.Errors).ErrorMessage);
    }

    private FluentValidation.Results.ValidationResult Validate(
        ReminderKind kind = ReminderKind.Measurement, string? title = "Tansiyon ölç", int intervalHours = 24, DateTime? firstDueAt = null) =>
        new ReminderInputValidator(_time).Validate(ReminderInput.Create(1, kind, title, intervalHours, firstDueAt ?? Now.AddHours(1)));
}

public sealed class ReminderServiceTests
{
    private static readonly DateTime Start = new(2025, 6, 15, 8, 0, 0, DateTimeKind.Unspecified);

    private readonly InMemoryUserRepository _users = new();
    private readonly InMemoryReminderRepository _reminders = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Start, TimeSpan.Zero));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ReminderServiceTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
        _users.Users.Add(new User { Id = 1, Name = "Ali", Age = 40, Gender = Genders.Male });
    }

    [Fact]
    public async Task Add_StoresTrimmedTitleAndMinutePrecision()
    {
        var reminder = await Service().AddAsync(1, ReminderKind.Exercise, "  Yürüyüş ", 24, Start.AddHours(1).AddSeconds(30), Ct);

        Assert.Equal("Yürüyüş", reminder.Title);
        Assert.Equal(Start.AddHours(1), Assert.Single(_reminders.Reminders).NextDueAt);
    }

    [Fact]
    public async Task Add_UnknownUser_StoresNothing()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service().AddAsync(9, ReminderKind.Measurement, "Ölç", 24, Start.AddHours(1), Ct));
        Assert.Empty(_reminders.Reminders);
    }

    [Fact]
    public async Task GetDue_ListsRemindersMissedWhileClosed_WithUserNameAndCount()
    {
        var service = Service();
        await service.AddAsync(1, ReminderKind.Measurement, "Kilo ölç", 24, Start.AddHours(1), Ct);
        await service.AddAsync(1, ReminderKind.Exercise, "Koşu", 24, Start.AddDays(10), Ct);

        _time.Advance(TimeSpan.FromDays(2).Add(TimeSpan.FromHours(1))); // Closed for two days.

        var due = Assert.Single(await service.GetDueAsync(Ct));
        Assert.Equal("Kilo ölç", due.Reminder.Title);
        Assert.Equal("Ali", due.UserName);
        Assert.True(due.IsDue);
        Assert.Equal(3, due.DueOccurrences);
        Assert.Equal(2, (await service.GetAllAsync(Ct)).Count);
    }

    [Fact]
    public async Task Complete_MovesToTheNextTimeAfterNow_AndASecondClickChangesNothing()
    {
        var service = Service();
        var reminder = await service.AddAsync(1, ReminderKind.Measurement, "Nabız ölç", 24, Start.AddHours(1), Ct);
        var dueAt = reminder.NextDueAt;
        _time.Advance(TimeSpan.FromDays(1).Add(TimeSpan.FromHours(2)));

        Assert.True(await service.CompleteAsync(reminder.Id, dueAt, Ct));
        Assert.False(await service.CompleteAsync(reminder.Id, dueAt, Ct));

        Assert.Equal(Start.AddHours(1).AddDays(2), Assert.Single(_reminders.Reminders).NextDueAt);
        Assert.Empty(await service.GetDueAsync(Ct));
    }

    [Fact]
    public async Task Complete_NotYetDueOrUnknown_ReturnsFalse()
    {
        var service = Service();
        var reminder = await service.AddAsync(1, ReminderKind.Measurement, "Ölç", 24, Start.AddHours(1), Ct);

        Assert.False(await service.CompleteAsync(reminder.Id, reminder.NextDueAt, Ct));
        Assert.False(await service.CompleteAsync(99, reminder.NextDueAt, Ct));
        Assert.Equal(Start.AddHours(1), Assert.Single(_reminders.Reminders).NextDueAt);
    }

    private ReminderService Service() => new(_users, _reminders, new ReminderInputValidator(_time), _time);
}

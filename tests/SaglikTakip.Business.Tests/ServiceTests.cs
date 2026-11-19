using Microsoft.Extensions.Time.Testing;
using SaglikTakip.Business.Services;
using SaglikTakip.Business.Validation;
using SaglikTakip.Domain.Entities;

namespace SaglikTakip.Business.Tests;

public sealed class ServiceTests
{
    private readonly InMemoryUserRepository _users = new();
    private readonly InMemoryHealthRecordRepository _records = new();
    private readonly InMemoryExerciseRepository _exercises = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task AddUser_TrimsNameAndStoresUser()
    {
        var service = NewUserService();

        var user = await service.AddAsync("  Deneme Kişi ", 30, Genders.Female, TestContext.Current.CancellationToken);

        Assert.Equal("Deneme Kişi", user.Name);
        Assert.Equal("Deneme Kişi - 30 Yaş - Kadın", Assert.Single(_users.Users).DisplayName);
    }

    [Theory]
    [InlineData("", 30, Genders.Male)]
    [InlineData("Ad", -1, Genders.Male)]
    [InlineData("Ad", 151, Genders.Male)]
    [InlineData("Ad", 30, null)]
    [InlineData("Ad", 30, "Bilinmiyor")]
    public async Task AddUser_InvalidInput_IsRejected(string name, int age, string? gender)
    {
        var service = NewUserService();

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.AddAsync(name, age, gender, TestContext.Current.CancellationToken));
        Assert.Empty(_users.Users);
    }

    [Fact]
    public async Task AddHealthRecord_StoresDateWithoutTimeAndBlankNotesAsNull()
    {
        var userId = await AddUserAsync();
        var service = new HealthRecordService(_users, _records, new HealthRecordInputValidator(_time));

        var record = await service.AddAsync(userId, new DateTime(2025, 6, 1, 17, 45, 0), 72.5m, 80, "   ", TestContext.Current.CancellationToken);

        Assert.Equal(new DateTime(2025, 6, 1), record.Date);
        Assert.Equal(72.5, record.Weight);
        Assert.Null(record.Notes);
    }

    [Theory]
    [InlineData(0, 80)]
    [InlineData(201, 80)]
    [InlineData(70, 0)]
    [InlineData(70, 251)]
    public async Task AddHealthRecord_OutOfRangeValues_AreRejected(int weight, int pulse)
    {
        var userId = await AddUserAsync();
        var service = new HealthRecordService(_users, _records, new HealthRecordInputValidator(_time));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.AddAsync(userId, new DateTime(2025, 6, 1), weight, pulse, null, TestContext.Current.CancellationToken));
        Assert.Empty(_records.Records);
    }

    [Fact]
    public async Task AddHealthRecord_FutureDateOrUnknownUser_IsRejected()
    {
        var userId = await AddUserAsync();
        var service = new HealthRecordService(_users, _records, new HealthRecordInputValidator(_time));
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AddAsync(userId, new DateTime(2025, 6, 16), 70, 80, null, ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AddAsync(userId + 1, new DateTime(2025, 6, 1), 70, 80, null, ct));
        Assert.Empty(_records.Records);
    }

    [Fact]
    public async Task AddExercise_RequiresNameAndSomeEffort()
    {
        var userId = await AddUserAsync();
        var service = new ExerciseService(_users, _exercises, new ExerciseInputValidator());
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AddAsync(userId, " ", 10, 0, ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AddAsync(userId, "Koşu", 0, 0, ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AddAsync(userId, "Koşu", -1, 5, ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AddAsync(0, "Koşu", 10, 0, ct));

        var exercise = await service.AddAsync(userId, " Koşu ", 30, 0, ct);
        Assert.Equal("Koşu", Assert.Single(_exercises.Exercises).Name);
        Assert.Equal(30, exercise.DurationMinutes);
    }

    [Fact]
    public async Task Report_ReturnsNewestRecordAndExerciseFirst()
    {
        var userId = await AddUserAsync();
        var otherUserId = await AddUserAsync();
        var ct = TestContext.Current.CancellationToken;
        var records = new HealthRecordService(_users, _records, new HealthRecordInputValidator(_time));
        var exercises = new ExerciseService(_users, _exercises, new ExerciseInputValidator());

        await records.AddAsync(userId, new DateTime(2025, 5, 1), 70, 80, "eski", ct);
        await records.AddAsync(userId, new DateTime(2025, 6, 1), 69, 75, "yeni", ct);
        await records.AddAsync(userId, new DateTime(2025, 4, 1), 71, 82, "en eski", ct);
        await records.AddAsync(otherUserId, new DateTime(2025, 6, 10), 90, 90, null, ct);
        await exercises.AddAsync(userId, "Yürüyüş", 30, 0, ct);
        await exercises.AddAsync(userId, "Şınav", 0, 20, ct);

        var report = await new ReportService(_users, _records, _exercises).GetUserReportAsync(userId, ct);

        Assert.Equal(["yeni", "eski", "en eski"], report.HealthRecords.Select(r => r.Notes));
        Assert.Equal("yeni", report.LatestHealthRecord?.Notes);
        Assert.Equal("Şınav", report.LatestExercise?.Name);
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => new ReportService(_users, _records, _exercises).GetUserReportAsync(999, ct));
    }

    private UserService NewUserService() => new(_users, new UserInputValidator());

    private async Task<int> AddUserAsync() =>
        (await NewUserService().AddAsync("Test Kullanıcı", 40, Genders.Other, TestContext.Current.CancellationToken)).Id;
}

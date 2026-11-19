using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Moq;
using SaglikTakip.Business.Services;
using SaglikTakip.Business.Validation;
using SaglikTakip.Domain.Entities;
using SaglikTakip.Domain.Repositories;

namespace SaglikTakip.Business.Tests;

/// <summary>Service tests with strict mocks: invalid input must never reach a repository.</summary>
public sealed class ServiceMockTests
{
    private static readonly DateTime Today = new(2025, 6, 15, 0, 0, 0, DateTimeKind.Unspecified);

    private readonly Mock<IUserRepository> _users = new(MockBehavior.Strict);
    private readonly Mock<IHealthRecordRepository> _records = new(MockBehavior.Strict);
    private readonly Mock<IExerciseRepository> _exercises = new(MockBehavior.Strict);
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AddUser_ValidInput_StoresTrimmedValues()
    {
        User? stored = null;
        _users.Setup(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => stored = user)
            .ReturnsAsync(7);

        var result = await UserService().AddAsync(" Deneme Kişi ", 150, " Diğer ", Ct);

        Assert.Equal(7, result.Id);
        Assert.NotNull(stored);
        Assert.Equal("Deneme Kişi", stored.Name);
        Assert.Equal(150, stored.Age);
        Assert.Equal(Genders.Other, stored.Gender);
    }

    [Theory]
    [InlineData("", 30, Genders.Male, "Ad alanı boş bırakılamaz.")]
    [InlineData("Ad", -1, Genders.Male, "Yaş 0 ile 150 arasında olmalıdır.")]
    [InlineData("Ad", 151, Genders.Male, "Yaş 0 ile 150 arasında olmalıdır.")]
    [InlineData("Ad", 30, null, "Lütfen cinsiyet seçin.")]
    public async Task AddUser_InvalidInput_IsRejectedWithoutTouchingTheRepository(
        string name, int age, string? gender, string expected)
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => UserService().AddAsync(name, age, gender, Ct));

        Assert.Equal(expected, error.Message);
        _users.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AddUser_AgeZero_IsAccepted()
    {
        _users.Setup(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var user = await UserService().AddAsync("Bebek", 0, Genders.Female, Ct);

        Assert.Equal(0, user.Age);
    }

    [Fact]
    public async Task AddHealthRecord_ValidInput_StoresDayWeightPulseAndTrimmedNotes()
    {
        HealthRecord? stored = null;
        _users.Setup(u => u.ExistsAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _records.Setup(r => r.AddAsync(It.IsAny<HealthRecord>(), It.IsAny<CancellationToken>()))
            .Callback<HealthRecord, CancellationToken>((record, _) => stored = record)
            .ReturnsAsync(11);

        var result = await HealthRecordService().AddAsync(3, Today.AddHours(20), 200m, 250, "  kontrol ", Ct);

        Assert.Equal(11, result.Id);
        Assert.NotNull(stored);
        Assert.Equal(3, stored.UserId);
        Assert.Equal(Today, stored.Date);
        Assert.Equal(200d, stored.Weight);
        Assert.Equal(250, stored.Pulse);
        Assert.Equal("kontrol", stored.Notes);
    }

    [Theory]
    [InlineData(0.5, 80, "Kilo 1 ile 200 arasında olmalıdır.")]
    [InlineData(200.5, 80, "Kilo 1 ile 200 arasında olmalıdır.")]
    [InlineData(70, 0, "Nabız 1 ile 250 arasında olmalıdır.")]
    [InlineData(70, 251, "Nabız 1 ile 250 arasında olmalıdır.")]
    public async Task AddHealthRecord_OutOfRangeValues_AreRejectedBeforeAnyLookup(double weight, int pulse, string expected)
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => HealthRecordService().AddAsync(3, Today, (decimal)weight, pulse, null, Ct));

        Assert.Equal(expected, error.Message);
        _users.VerifyNoOtherCalls();
        _records.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AddHealthRecord_FutureDate_IsRejected()
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => HealthRecordService().AddAsync(3, Today.AddDays(1), 70m, 80, null, Ct));

        Assert.Equal("Sağlık kaydının tarihi ileri bir tarih olamaz.", error.Message);
        _records.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AddHealthRecord_NoUserSelected_IsRejected()
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => HealthRecordService().AddAsync(0, Today, 70m, 80, null, Ct));

        Assert.Equal("Lütfen bir kullanıcı seçin.", error.Message);
        _users.VerifyNoOtherCalls();
        _records.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AddHealthRecord_UnknownUser_IsRejected()
    {
        _users.Setup(u => u.ExistsAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => HealthRecordService().AddAsync(9, Today, 70m, 80, null, Ct));

        Assert.Equal("Seçilen kullanıcı bulunamadı.", error.Message);
        _records.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AddExercise_ValidInput_IsStored()
    {
        Exercise? stored = null;
        _users.Setup(u => u.ExistsAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _exercises.Setup(e => e.AddAsync(It.IsAny<Exercise>(), It.IsAny<CancellationToken>()))
            .Callback<Exercise, CancellationToken>((exercise, _) => stored = exercise)
            .ReturnsAsync(4);

        var result = await ExerciseService().AddAsync(2, " Şınav ", 0, 1000, Ct);

        Assert.Equal(4, result.Id);
        Assert.NotNull(stored);
        Assert.Equal("Şınav", stored.Name);
        Assert.Equal(0, stored.DurationMinutes);
        Assert.Equal(1000, stored.Repetitions);
    }

    [Theory]
    [InlineData(" ", 10, 0, "Egzersiz adı alanı boş bırakılamaz.")]
    [InlineData("Koşu", 0, 0, "Süre veya tekrar sayısından en az biri sıfırdan büyük olmalıdır.")]
    [InlineData("Koşu", 1441, 0, "Süre (dakika) 0 ile 1440 arasında olmalıdır.")]
    [InlineData("Koşu", 10, -1, "Tekrar sayısı 0 ile 1000 arasında olmalıdır.")]
    public async Task AddExercise_InvalidInput_IsRejectedWithoutTouchingTheRepositories(
        string name, int duration, int repetitions, string expected)
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => ExerciseService().AddAsync(2, name, duration, repetitions, Ct));

        Assert.Equal(expected, error.Message);
        _users.VerifyNoOtherCalls();
        _exercises.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AddExercise_UnknownUser_IsRejected()
    {
        _users.Setup(u => u.ExistsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => ExerciseService().AddAsync(5, "Koşu", 10, 0, Ct));

        Assert.Equal("Seçilen kullanıcı bulunamadı.", error.Message);
        _exercises.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Report_UnknownUser_IsRejected()
    {
        _users.Setup(u => u.GetByIdAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => new ReportService(_users.Object, _records.Object, _exercises.Object).GetUserReportAsync(8, Ct));

        Assert.Equal("Seçilen kullanıcı bulunamadı.", error.Message);
        _records.VerifyNoOtherCalls();
        _exercises.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Report_CombinesUserRecordsAndExercises()
    {
        var user = new User { Id = 1, Name = "Örnek", Age = 30, Gender = Genders.Female };
        HealthRecord[] records = [new() { Id = 2, UserId = 1, Date = Today, Weight = 60, Pulse = 70 }];
        Exercise[] exercises = [new() { Id = 3, UserId = 1, Name = "Yürüyüş", DurationMinutes = 20 }];
        _users.Setup(u => u.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _records.Setup(r => r.GetByUserAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(records);
        _exercises.Setup(e => e.GetByUserAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(exercises);

        var report = await new ReportService(_users.Object, _records.Object, _exercises.Object).GetUserReportAsync(1, Ct);

        Assert.Same(user, report.User);
        Assert.Same(records[0], report.LatestHealthRecord);
        Assert.Same(exercises[0], report.LatestExercise);
    }

    [Fact]
    public void AddBusinessServices_ResolvesServicesWithValidators()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_users.Object);
        services.AddSingleton(_records.Object);
        services.AddSingleton(_exercises.Object);
        services.AddSingleton(new Mock<IHealthGoalRepository>(MockBehavior.Strict).Object);
        services.AddBusinessServices();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        Assert.NotNull(provider.GetRequiredService<IUserService>());
        Assert.NotNull(provider.GetRequiredService<IHealthRecordService>());
        Assert.NotNull(provider.GetRequiredService<IExerciseService>());
        Assert.NotNull(provider.GetRequiredService<IReportService>());
        Assert.NotNull(provider.GetRequiredService<ITrendService>());
        Assert.NotNull(provider.GetRequiredService<IGoalService>());
    }

    private UserService UserService() => new(_users.Object, new UserInputValidator());

    private HealthRecordService HealthRecordService() =>
        new(_users.Object, _records.Object, new HealthRecordInputValidator(_time));

    private ExerciseService ExerciseService() => new(_users.Object, _exercises.Object, new ExerciseInputValidator());
}

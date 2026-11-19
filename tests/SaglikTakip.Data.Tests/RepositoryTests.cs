using Microsoft.EntityFrameworkCore;
using SaglikTakip.Business;
using SaglikTakip.Business.Services;
using SaglikTakip.Domain.Entities;
using SaglikTakip.Domain.Repositories;

namespace SaglikTakip.Data.Tests;

/// <summary>Repositories and services against SQLite in-memory (schema from the EF Core model).</summary>
public sealed class RepositoryTests : IAsyncLifetime
{
    private readonly SqliteDatabase _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => _db.InitializeAsync();

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task Users_SeedDataIsPresentAndOrderedById()
    {
        var users = await _db.Get<IUserRepository>().GetAllAsync(Ct);

        Assert.Equal([1, 2, 3], users.Select(u => u.Id));
        Assert.All(users, u => Assert.Contains(u.Gender, Genders.All));
    }

    [Fact]
    public async Task Users_AddThenFindById()
    {
        var repository = _db.Get<IUserRepository>();

        var id = await repository.AddAsync(new User { Name = "Yeni Kişi", Age = 150, Gender = Genders.Male }, Ct);

        var user = await repository.GetByIdAsync(id, Ct);
        Assert.NotNull(user);
        Assert.Equal("Yeni Kişi", user.Name);
        Assert.Equal(150, user.Age);
        Assert.True(await repository.ExistsAsync(id, Ct));
        Assert.False(await repository.ExistsAsync(id + 100, Ct));
        Assert.Null(await repository.GetByIdAsync(id + 100, Ct));
    }

    [Theory]
    [InlineData("Bilinmiyor", 30)]
    [InlineData(Genders.Female, 151)]
    public async Task Users_OutsideTheSchemaLimits_AreRefusedByCheckConstraints(string gender, int age)
    {
        await Assert.ThrowsAsync<DbUpdateException>(() => _db.Get<IUserRepository>()
            .AddAsync(new User { Name = "Sınır", Age = age, Gender = gender }, Ct));
    }

    [Fact]
    public async Task HealthRecords_AreFilteredByUserAndSortedNewestDateFirst()
    {
        var repository = _db.Get<IHealthRecordRepository>();
        var userId = await _db.Get<IUserRepository>().AddAsync(new User { Name = "Kayıt", Age = 40, Gender = Genders.Other }, Ct);

        await repository.AddAsync(Record(userId, new DateTime(2025, 5, 1), "orta"), Ct);
        await repository.AddAsync(Record(userId, new DateTime(2025, 6, 1), "yeni-1"), Ct);
        await repository.AddAsync(Record(userId, new DateTime(2025, 4, 1), "eski"), Ct);
        await repository.AddAsync(Record(userId, new DateTime(2025, 6, 1), "yeni-2"), Ct);

        var records = await repository.GetByUserAsync(userId, Ct);

        // Same day: the newest entry comes first.
        Assert.Equal(["yeni-2", "yeni-1", "orta", "eski"], records.Select(r => r.Notes));
        Assert.Equal(new DateTime(2025, 6, 1), records[0].Date);
        Assert.All(records, r => Assert.Equal(userId, r.UserId));
    }

    [Fact]
    public async Task HealthRecords_Between_IncludeBothEndDaysOldestFirst()
    {
        var repository = _db.Get<IHealthRecordRepository>();
        var userId = await _db.Get<IUserRepository>().AddAsync(new User { Name = "Aralık", Age = 40, Gender = Genders.Other }, Ct);

        await repository.AddAsync(Record(userId, new DateTime(2025, 5, 31), "önce"), Ct);
        await repository.AddAsync(Record(userId, new DateTime(2025, 6, 10), "son-1"), Ct);
        await repository.AddAsync(Record(userId, new DateTime(2025, 6, 1), "ilk"), Ct);
        await repository.AddAsync(Record(userId, new DateTime(2025, 6, 10), "son-2"), Ct);
        await repository.AddAsync(Record(userId, new DateTime(2025, 6, 11), "sonra"), Ct);

        var records = await repository.GetByUserBetweenAsync(
            userId, new DateTime(2025, 6, 1, 18, 0, 0), new DateTime(2025, 6, 10, 6, 0, 0), Ct);

        Assert.Equal(["ilk", "son-1", "son-2"], records.Select(r => r.Notes));
        Assert.Empty(await repository.GetByUserBetweenAsync(userId, new DateTime(2025, 7, 1), new DateTime(2025, 7, 31), Ct));
    }

    [Fact]
    public async Task HealthRecords_KeepWeightPulseAndNullNotes()
    {
        var repository = _db.Get<IHealthRecordRepository>();

        var id = await repository.AddAsync(
            new HealthRecord { UserId = 2, Date = new DateTime(2025, 6, 2), Weight = 72.5, Pulse = 250, Notes = null }, Ct);

        var record = (await repository.GetByUserAsync(2, Ct)).Single(r => r.Id == id);
        Assert.Equal(72.5, record.Weight);
        Assert.Equal(250, record.Pulse);
        Assert.Null(record.Notes);
    }

    [Fact]
    public async Task HealthRecords_ForUnknownUser_AreRefusedByTheForeignKey()
    {
        await Assert.ThrowsAsync<DbUpdateException>(
            () => _db.Get<IHealthRecordRepository>().AddAsync(Record(999, new DateTime(2025, 1, 1), null), Ct));
    }

    [Theory]
    [InlineData(201, 80)]
    [InlineData(70, 251)]
    public async Task HealthRecords_OutsideTheSchemaLimits_AreRefusedByCheckConstraints(double weight, int pulse)
    {
        var record = new HealthRecord { UserId = 1, Date = new DateTime(2025, 1, 1), Weight = weight, Pulse = pulse };

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.Get<IHealthRecordRepository>().AddAsync(record, Ct));
    }

    [Fact]
    public async Task Exercises_AreFilteredByUserAndSortedNewestFirst()
    {
        var repository = _db.Get<IExerciseRepository>();

        await repository.AddAsync(new Exercise { UserId = 3, Name = "Koşu", DurationMinutes = 30 }, Ct);
        await repository.AddAsync(new Exercise { UserId = 3, Name = "Mekik", Repetitions = 40 }, Ct);

        var exercises = await repository.GetByUserAsync(3, Ct);

        Assert.Equal(["Mekik", "Koşu", "Şınav"], exercises.Select(e => e.Name));
        Assert.Empty(await repository.GetByUserAsync(999, Ct));
    }

    [Fact]
    public async Task Services_StoreValidatedInputAndReportIt()
    {
        var user = await _db.Get<IUserService>().AddAsync("  Rapor Kişi ", 0, Genders.Female, Ct);
        var today = DateTime.Today;
        await _db.Get<IHealthRecordService>().AddAsync(user.Id, today.AddHours(9), 1m, 1, "  sabah ", Ct);
        await _db.Get<IExerciseService>().AddAsync(user.Id, " Esneme ", 1440, 0, Ct);

        var report = await _db.Get<IReportService>().GetUserReportAsync(user.Id, Ct);

        Assert.Equal("Rapor Kişi", report.User.Name);
        var record = Assert.Single(report.HealthRecords);
        Assert.Equal(today, record.Date);
        Assert.Equal(1d, record.Weight);
        Assert.Equal("sabah", record.Notes);
        Assert.Equal("Esneme", Assert.Single(report.Exercises).Name);
    }

    [Fact]
    public async Task Services_RejectInvalidInputWithoutWriting()
    {
        var ct = Ct;
        var records = _db.Get<IHealthRecordService>();

        await Assert.ThrowsAsync<BusinessRuleException>(() => records.AddAsync(1, DateTime.Today.AddDays(1), 70m, 80, null, ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => records.AddAsync(1, DateTime.Today, 200.01m, 80, null, ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => records.AddAsync(999, DateTime.Today, 70m, 80, null, ct));

        await using var context = _db.CreateContext();
        Assert.Equal(4, await context.HealthRecords.CountAsync(ct)); // Only the seed data.
    }

    private static HealthRecord Record(int userId, DateTime date, string? notes) =>
        new() { UserId = userId, Date = date, Weight = 70, Pulse = 80, Notes = notes };
}

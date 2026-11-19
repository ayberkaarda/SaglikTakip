using Microsoft.EntityFrameworkCore;
using SaglikTakip.Domain.Entities;
using SaglikTakip.Domain.Repositories;

namespace SaglikTakip.Data.Tests;

/// <summary>Reminders on SQLite in-memory (schema from the EF Core model).</summary>
public sealed class ReminderRepositoryTests : IAsyncLifetime
{
    private static readonly DateTime Nine = new(2025, 6, 15, 9, 0, 0, DateTimeKind.Unspecified);

    private readonly SqliteDatabase _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => _db.InitializeAsync();

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task GetDue_IncludesTheExactDueTime_AndIsOrderedEarliestFirst()
    {
        var repository = _db.Get<IReminderRepository>();
        await repository.AddAsync(Reminder("sonra", Nine.AddMinutes(1)), Ct);
        await repository.AddAsync(Reminder("tam", Nine), Ct);
        await repository.AddAsync(Reminder("önce", Nine.AddHours(-3)), Ct);

        var due = await repository.GetDueAsync(Nine, Ct);

        Assert.Equal(["önce", "tam"], due.Select(r => r.Title));
        Assert.Equal(["önce", "tam", "sonra"], (await repository.GetAllAsync(Ct)).Select(r => r.Title));
    }

    [Fact]
    public async Task Reschedule_OnlyWhileStillDueAtTheExpectedTime()
    {
        var repository = _db.Get<IReminderRepository>();
        var id = await repository.AddAsync(Reminder("ölç", Nine), Ct);

        Assert.True(await repository.RescheduleAsync(id, Nine, Nine.AddDays(1), Ct));
        Assert.False(await repository.RescheduleAsync(id, Nine, Nine.AddDays(2), Ct)); // Stale second click.

        var stored = await repository.GetByIdAsync(id, Ct);
        Assert.Equal(Nine.AddDays(1), stored?.NextDueAt);
        Assert.Equal(ReminderKind.Exercise, stored?.Kind);
    }

    [Fact]
    public async Task Delete_RemovesTheReminder()
    {
        var repository = _db.Get<IReminderRepository>();
        var id = await repository.AddAsync(Reminder("sil", Nine), Ct);

        Assert.True(await repository.DeleteAsync(id, Ct));
        Assert.False(await repository.DeleteAsync(id, Ct));
        Assert.Null(await repository.GetByIdAsync(id, Ct));
    }

    [Theory]
    [InlineData(0, ReminderKind.Measurement)]
    [InlineData(8761, ReminderKind.Measurement)]
    [InlineData(24, (ReminderKind)5)]
    public async Task Add_OutsideTheSchemaLimits_IsRefusedByCheckConstraints(int intervalHours, ReminderKind kind)
    {
        var reminder = Reminder("x", Nine);
        reminder.IntervalHours = intervalHours;
        reminder.Kind = kind;

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.Get<IReminderRepository>().AddAsync(reminder, Ct));
    }

    private static Reminder Reminder(string title, DateTime dueAt) =>
        new() { UserId = 1, Kind = ReminderKind.Exercise, Title = title, IntervalHours = 24, NextDueAt = dueAt };
}

using Microsoft.EntityFrameworkCore;
using SaglikTakip.Business.Services;
using SaglikTakip.Business.Validation;
using SaglikTakip.Domain.Entities;
using SaglikTakip.Domain.Repositories;

namespace SaglikTakip.Data.Tests;

/// <summary>Goals on SQLite in-memory (schema from the EF Core model).</summary>
public sealed class GoalRepositoryTests : IAsyncLifetime
{
    private readonly SqliteDatabase _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => _db.InitializeAsync();

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task Save_CreatesThenReplacesTheUsersOnlyGoal()
    {
        var repository = _db.Get<IHealthGoalRepository>();

        var id = await repository.SaveAsync(new HealthGoal { UserId = 1, TargetWeight = 60, MinPulse = 50, MaxPulse = 120 }, Ct);
        var again = await repository.SaveAsync(new HealthGoal { UserId = 1, TargetWeight = 58.5, MaxWeight = 70 }, Ct);

        Assert.Equal(id, again);
        var goal = await repository.GetByUserAsync(1, Ct);
        Assert.NotNull(goal);
        Assert.Equal(58.5, goal.TargetWeight);
        Assert.Equal(70, goal.MaxWeight);
        Assert.Null(goal.MinPulse);
        Assert.Null(goal.MaxPulse);
        Assert.Null(await repository.GetByUserAsync(2, Ct));

        await using var context = _db.CreateContext();
        Assert.Equal(1, await context.HealthGoals.CountAsync(Ct));
    }

    [Fact]
    public async Task Delete_RemovesOnlyThatUsersGoal()
    {
        var repository = _db.Get<IHealthGoalRepository>();
        await repository.SaveAsync(new HealthGoal { UserId = 1, TargetWeight = 60 }, Ct);
        await repository.SaveAsync(new HealthGoal { UserId = 2, TargetWeight = 80 }, Ct);

        Assert.True(await repository.DeleteByUserAsync(1, Ct));
        Assert.False(await repository.DeleteByUserAsync(1, Ct));
        Assert.NotNull(await repository.GetByUserAsync(2, Ct));
    }

    [Theory]
    [InlineData(80.0, 70.0, null, null)]
    [InlineData(null, null, 120, 60)]
    [InlineData(0.5, null, null, null)]
    [InlineData(null, null, null, 251)]
    public async Task Save_BrokenLimits_AreRefusedByCheckConstraints(double? minWeight, double? maxWeight, int? minPulse, int? maxPulse)
    {
        var goal = new HealthGoal { UserId = 1, MinWeight = minWeight, MaxWeight = maxWeight, MinPulse = minPulse, MaxPulse = maxPulse };

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.Get<IHealthGoalRepository>().SaveAsync(goal, Ct));
    }

    [Fact]
    public async Task Save_UnknownUser_IsRefusedByTheForeignKey()
    {
        await Assert.ThrowsAsync<DbUpdateException>(
            () => _db.Get<IHealthGoalRepository>().SaveAsync(new HealthGoal { UserId = 999, TargetWeight = 70 }, Ct));
    }

    [Fact]
    public async Task Service_StatusOnSeedData()
    {
        var goals = _db.Get<IGoalService>();
        await goals.SaveAsync(1, new GoalInput(60, 65, null, null, null, 71), Ct);

        // Seed: user 1 has 64 kg / 72 bpm on 2025-03-03 and 63 kg / 70 bpm on 2025-04-07.
        var status = await goals.GetStatusAsync(1, new DateTime(2025, 1, 1), new DateTime(2025, 12, 31), Ct);

        Assert.NotNull(status);
        Assert.Equal(63, status.CurrentWeight);
        Assert.Equal(40, status.ProgressPercent!.Value, 9);
        Assert.Equal(new DateTime(2025, 3, 3), Assert.Single(status.Alerts).Date);
    }
}

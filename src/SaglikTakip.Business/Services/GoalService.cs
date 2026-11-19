using FluentValidation;
using SaglikTakip.Business.Insights;
using SaglikTakip.Business.Validation;
using SaglikTakip.Domain.Entities;
using SaglikTakip.Domain.Repositories;

namespace SaglikTakip.Business.Services;

public interface IGoalService
{
    /// <summary>The user's goal, or <see langword="null"/> when none is set.</summary>
    Task<HealthGoal?> GetAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Creates the user's goal or replaces all of its values.</summary>
    Task<HealthGoal> SaveAsync(int userId, GoalInput input, CancellationToken cancellationToken = default);

    /// <summary>Removes the user's goal; <see langword="false"/> when there was none.</summary>
    Task<bool> DeleteAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The goal evaluated against the measurements from <paramref name="firstDay"/> to
    /// <paramref name="lastDay"/> (whole days, both included); <see langword="null"/> when no goal is set.
    /// </summary>
    Task<GoalStatus?> GetStatusAsync(int userId, DateTime firstDay, DateTime lastDay, CancellationToken cancellationToken = default);

    /// <summary>Limits that <paramref name="record"/> breaks; empty when it has none or no goal is set.</summary>
    Task<IReadOnlyList<ThresholdAlert>> CheckAsync(HealthRecord record, CancellationToken cancellationToken = default);
}

internal sealed class GoalService(
    IUserRepository users,
    IHealthRecordRepository records,
    IHealthGoalRepository goals,
    IValidator<GoalInput> validator) : IGoalService
{
    public async Task<HealthGoal?> GetAsync(int userId, CancellationToken cancellationToken = default)
    {
        await UserGuard.EnsureExistsAsync(users, userId, cancellationToken).ConfigureAwait(false);
        return await goals.GetByUserAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<HealthGoal> SaveAsync(int userId, GoalInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        validator.ValidateOrThrow(input);

        await UserGuard.EnsureExistsAsync(users, userId, cancellationToken).ConfigureAwait(false);

        var goal = new HealthGoal
        {
            UserId = userId,
            TargetWeight = (double?)input.TargetWeight,
            StartWeight = (double?)input.StartWeight,
            MinWeight = (double?)input.MinWeight,
            MaxWeight = (double?)input.MaxWeight,
            MinPulse = input.MinPulse,
            MaxPulse = input.MaxPulse,
        };
        goal.Id = await goals.SaveAsync(goal, cancellationToken).ConfigureAwait(false);
        return goal;
    }

    public async Task<bool> DeleteAsync(int userId, CancellationToken cancellationToken = default)
    {
        await UserGuard.EnsureExistsAsync(users, userId, cancellationToken).ConfigureAwait(false);
        return await goals.DeleteByUserAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<GoalStatus?> GetStatusAsync(
        int userId,
        DateTime firstDay,
        DateTime lastDay,
        CancellationToken cancellationToken = default)
    {
        var range = RangeGuard.Days(firstDay, lastDay);
        await UserGuard.EnsureExistsAsync(users, userId, cancellationToken).ConfigureAwait(false);

        var goal = await goals.GetByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        if (goal is null)
        {
            return null;
        }

        var inRange = await records.GetByUserBetweenAsync(userId, range.From, range.To, cancellationToken).ConfigureAwait(false);
        return GoalEvaluator.Evaluate(goal, inRange);
    }

    public async Task<IReadOnlyList<ThresholdAlert>> CheckAsync(HealthRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var goal = await goals.GetByUserAsync(record.UserId, cancellationToken).ConfigureAwait(false);
        return goal is null ? [] : GoalEvaluator.Check(goal, record);
    }
}

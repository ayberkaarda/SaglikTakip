using FluentValidation;
using SaglikTakip.Business.Validation;
using SaglikTakip.Domain.Entities;
using SaglikTakip.Domain.Repositories;

namespace SaglikTakip.Business.Services;

public interface IExerciseService
{
    Task<Exercise> AddAsync(
        int userId,
        string? name,
        int durationMinutes,
        int repetitions,
        CancellationToken cancellationToken = default);
}

internal sealed class ExerciseService(
    IUserRepository users,
    IExerciseRepository exercises,
    IValidator<ExerciseInput> validator) : IExerciseService
{
    public async Task<Exercise> AddAsync(
        int userId,
        string? name,
        int durationMinutes,
        int repetitions,
        CancellationToken cancellationToken = default)
    {
        var input = ExerciseInput.Create(userId, name, durationMinutes, repetitions);
        validator.ValidateOrThrow(input);

        await UserGuard.EnsureExistsAsync(users, userId, cancellationToken).ConfigureAwait(false);

        var exercise = new Exercise
        {
            UserId = input.UserId,
            Name = input.Name,
            DurationMinutes = input.DurationMinutes,
            Repetitions = input.Repetitions,
        };

        exercise.Id = await exercises.AddAsync(exercise, cancellationToken).ConfigureAwait(false);
        return exercise;
    }
}

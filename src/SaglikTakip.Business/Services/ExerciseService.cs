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

internal sealed class ExerciseService(IUserRepository users, IExerciseRepository exercises) : IExerciseService
{
    internal const int MaxNameLength = 100;
    internal const int MaxDurationMinutes = 1440;
    internal const int MaxRepetitions = 1000;

    public async Task<Exercise> AddAsync(
        int userId,
        string? name,
        int durationMinutes,
        int repetitions,
        CancellationToken cancellationToken = default)
    {
        var exercise = new Exercise
        {
            UserId = userId,
            Name = Validation.Required(name, "Egzersiz adı", MaxNameLength),
            DurationMinutes = Validation.Range(durationMinutes, "Süre (dakika)", 0, MaxDurationMinutes),
            Repetitions = Validation.Range(repetitions, "Tekrar sayısı", 0, MaxRepetitions),
        };

        if (exercise.DurationMinutes == 0 && exercise.Repetitions == 0)
        {
            throw new BusinessRuleException("Süre veya tekrar sayısından en az biri sıfırdan büyük olmalıdır.");
        }

        await UserGuard.EnsureExistsAsync(users, userId, cancellationToken).ConfigureAwait(false);

        exercise.Id = await exercises.AddAsync(exercise, cancellationToken).ConfigureAwait(false);
        return exercise;
    }
}

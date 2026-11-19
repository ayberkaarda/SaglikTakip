using Microsoft.Extensions.DependencyInjection;
using SaglikTakip.Business;
using SaglikTakip.Domain.Entities;
using SaglikTakip.Domain.Repositories;

namespace SaglikTakip.Screenshots;

/// <summary>
/// Invented people and measurements for the screenshots. The names are made up and the values are
/// generated from a fixed seed relative to today, so every run shows a recent, realistic month.
/// </summary>
internal static class SyntheticData
{
    public static ServiceProvider CreateServices()
    {
        var today = DateTime.Today;
        var users = new InMemoryUserRepository();
        var records = new InMemoryHealthRecordRepository();
        var exercises = new InMemoryExerciseRepository();
        var goals = new InMemoryHealthGoalRepository();
        var reminders = new InMemoryReminderRepository();

        users.Users.AddRange(
        [
            new User { Id = 1, Name = "Elif Demir", Age = 34, Gender = Genders.Female },
            new User { Id = 2, Name = "Mert Kaya", Age = 41, Gender = Genders.Male },
            new User { Id = 3, Name = "Deniz Arslan", Age = 27, Gender = Genders.Other },
        ]);

        // About two measurements out of three days over the last month, slowly trending down.
        var random = new Random(20261119);
        var id = 1;
        for (var day = 34; day >= 0; day--)
        {
            if (day % 3 == 1 && day > 2)
            {
                continue;
            }

            var weight = 77.6 - ((34 - day) * 0.16) + ((random.NextDouble() - 0.5) * 0.9);
            var pulse = 72 + random.Next(-6, 7) + (day == 9 ? 17 : 0);
            records.Records.Add(new HealthRecord
            {
                Id = id++,
                UserId = 1,
                Date = today.AddDays(-day).AddHours(8),
                Weight = Math.Round(weight, 1),
                Pulse = pulse,
                Notes = day switch
                {
                    0 => "Sabah, kahvaltıdan önce",
                    9 => "Merdiven çıktıktan hemen sonra",
                    16 => "Hafta sonu, geç kalkıldı",
                    _ => null,
                },
            });
        }

        for (var day = 20; day >= 0; day -= 4)
        {
            records.Records.Add(new HealthRecord
            {
                Id = id++,
                UserId = 2,
                Date = today.AddDays(-day).AddHours(19),
                Weight = Math.Round(88.2 - ((20 - day) * 0.05), 1),
                Pulse = 66 + (day % 5),
            });
        }

        exercises.Exercises.AddRange(
        [
            new Exercise { Id = 1, UserId = 1, Name = "Tempolu yürüyüş", DurationMinutes = 40, Repetitions = 1 },
            new Exercise { Id = 2, UserId = 1, Name = "Esneme hareketleri", DurationMinutes = 15, Repetitions = 3 },
            new Exercise { Id = 3, UserId = 1, Name = "Bisiklet", DurationMinutes = 30, Repetitions = 1 },
            new Exercise { Id = 4, UserId = 1, Name = "Squat", DurationMinutes = 10, Repetitions = 20 },
            new Exercise { Id = 5, UserId = 1, Name = "Yüzme", DurationMinutes = 45, Repetitions = 1 },
            new Exercise { Id = 6, UserId = 2, Name = "Koşu", DurationMinutes = 25, Repetitions = 1 },
        ]);

        goals.Goals.Add(new HealthGoal
        {
            Id = 1,
            UserId = 1,
            TargetWeight = 70,
            StartWeight = 78,
            MinWeight = 70,
            MaxWeight = 76,
            MinPulse = 55,
            MaxPulse = 85,
        });

        var now = DateTime.Now;
        var nextMorning = today.AddDays(1).AddHours(8);
        reminders.Reminders.AddRange(
        [
            new Reminder { Id = 1, UserId = 1, Kind = ReminderKind.Measurement, Title = "Sabah kilo ölçümü", IntervalHours = 24, NextDueAt = now.AddHours(-2).AddMinutes(-now.Minute) },
            new Reminder { Id = 2, UserId = 2, Kind = ReminderKind.Measurement, Title = "Nabız ölçümü", IntervalHours = 24, NextDueAt = now.AddDays(-2).AddMinutes(-now.Minute) },
            new Reminder { Id = 3, UserId = 1, Kind = ReminderKind.Exercise, Title = "Akşam yürüyüşü", IntervalHours = 24, NextDueAt = today.AddHours(now.Hour < 19 ? 19 : 43) },
            new Reminder { Id = 4, UserId = 3, Kind = ReminderKind.Exercise, Title = "Esneme hareketleri", IntervalHours = 24 * 7, NextDueAt = nextMorning.AddDays(2) },
        ]);

        var services = new ServiceCollection();
        services.AddSingleton<IUserRepository>(users);
        services.AddSingleton<IHealthRecordRepository>(records);
        services.AddSingleton<IExerciseRepository>(exercises);
        services.AddSingleton<IHealthGoalRepository>(goals);
        services.AddSingleton<IReminderRepository>(reminders);
        services.AddBusinessServices();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }
}

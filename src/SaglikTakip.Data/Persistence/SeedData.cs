using SaglikTakip.Domain.Entities;

namespace SaglikTakip.Data.Persistence;

/// <summary>
/// Sample data applied by the InitialCreate migration through HasData. Values are fixed so the
/// migration is deterministic; the people are fictional.
/// </summary>
internal static class SeedData
{
    public static readonly User[] Users =
    [
        new() { Id = 1, Name = "Örnek Kullanıcı", Age = 34, Gender = Genders.Female },
        new() { Id = 2, Name = "Deneme Kişi", Age = 41, Gender = Genders.Male },
        new() { Id = 3, Name = "Test Sporcu", Age = 26, Gender = Genders.Other },
    ];

    public static readonly HealthRecord[] HealthRecords =
    [
        new() { Id = 1, UserId = 1, Date = Day(2025, 3, 3), Weight = 64, Pulse = 72, Notes = "Rutin kontrol" },
        new() { Id = 2, UserId = 1, Date = Day(2025, 4, 7), Weight = 63, Pulse = 70, Notes = null },
        new() { Id = 3, UserId = 2, Date = Day(2025, 3, 10), Weight = 88, Pulse = 78, Notes = "Diyete başlandı" },
        new() { Id = 4, UserId = 3, Date = Day(2025, 5, 2), Weight = 71, Pulse = 58, Notes = null },
    ];

    public static readonly Exercise[] Exercises =
    [
        new() { Id = 1, UserId = 1, Name = "Yürüyüş", DurationMinutes = 45, Repetitions = 1 },
        new() { Id = 2, UserId = 2, Name = "Bisiklet", DurationMinutes = 30, Repetitions = 1 },
        new() { Id = 3, UserId = 3, Name = "Şınav", DurationMinutes = 10, Repetitions = 20 },
    ];

    private static DateTime Day(int year, int month, int day) => new(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);
}

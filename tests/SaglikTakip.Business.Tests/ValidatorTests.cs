using Microsoft.Extensions.Time.Testing;
using SaglikTakip.Business.Validation;
using SaglikTakip.Domain.Entities;

namespace SaglikTakip.Business.Tests;

public sealed class ValidatorTests
{
    private static readonly DateTime Today = new(2025, 6, 15, 0, 0, 0, DateTimeKind.Unspecified);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero));

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(150, true)]
    [InlineData(151, false)]
    public void User_AgeBoundaries(int age, bool expectedValid)
    {
        var result = new UserInputValidator().Validate(UserInput.Create("Ad", age, Genders.Male));

        Assert.Equal(expectedValid, result.IsValid);
        if (!expectedValid)
        {
            Assert.Equal("Yaş 0 ile 150 arasında olmalıdır.", Assert.Single(result.Errors).ErrorMessage);
        }
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void User_NameLengthBoundary(int length, bool expectedValid)
    {
        var result = new UserInputValidator().Validate(UserInput.Create(new string('a', length), 30, Genders.Female));

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData(Genders.Male, true)]
    [InlineData(Genders.Female, true)]
    [InlineData(Genders.Other, true)]
    [InlineData(" Kadın ", true)] // Trimmed before validation.
    [InlineData("kadın", false)] // Stored values are case sensitive.
    [InlineData("Bilinmiyor", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void User_GenderMustBeOneOfTheKnownValues(string? gender, bool expectedValid)
    {
        var result = new UserInputValidator().Validate(UserInput.Create("Ad", 30, gender));

        Assert.Equal(expectedValid, result.IsValid);
        if (!expectedValid)
        {
            Assert.Equal("Lütfen cinsiyet seçin.", Assert.Single(result.Errors).ErrorMessage);
        }
    }

    [Fact]
    public void User_StopsAtFirstFailure()
    {
        var result = new UserInputValidator().Validate(UserInput.Create("  ", 999, "x"));

        Assert.Equal("Ad alanı boş bırakılamaz.", Assert.Single(result.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData(0.99, false)]
    [InlineData(1, true)]
    [InlineData(200, true)]
    [InlineData(200.01, false)]
    public void HealthRecord_WeightBoundaries(double weight, bool expectedValid)
    {
        var result = ValidateRecord(weight: (decimal)weight);

        Assert.Equal(expectedValid, result.IsValid);
        if (!expectedValid)
        {
            Assert.Equal("Kilo 1 ile 200 arasında olmalıdır.", Assert.Single(result.Errors).ErrorMessage);
        }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(250, true)]
    [InlineData(251, false)]
    public void HealthRecord_PulseBoundaries(int pulse, bool expectedValid)
    {
        var result = ValidateRecord(pulse: pulse);

        Assert.Equal(expectedValid, result.IsValid);
        if (!expectedValid)
        {
            Assert.Equal("Nabız 1 ile 250 arasında olmalıdır.", Assert.Single(result.Errors).ErrorMessage);
        }
    }

    [Fact]
    public void HealthRecord_TodayIsAccepted_EvenLateInTheDay()
    {
        Assert.True(ValidateRecord(date: Today.AddHours(23).AddMinutes(59)).IsValid);
    }

    [Fact]
    public void HealthRecord_TomorrowIsRejectedAsFutureDate()
    {
        var result = ValidateRecord(date: Today.AddDays(1));

        Assert.Equal("Sağlık kaydının tarihi ileri bir tarih olamaz.", Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public void HealthRecord_FutureLimitFollowsTheClock()
    {
        var validator = new HealthRecordInputValidator(_time);
        var tomorrow = HealthRecordInput.Create(1, Today.AddDays(1), 70, 80, null);

        Assert.False(validator.Validate(tomorrow).IsValid);
        _time.Advance(TimeSpan.FromDays(1));
        Assert.True(validator.Validate(tomorrow).IsValid);
    }

    [Theory]
    [InlineData(1899, 12, 31, false)]
    [InlineData(1900, 1, 1, true)]
    public void HealthRecord_MinimumDateBoundary(int year, int month, int day, bool expectedValid)
    {
        var result = ValidateRecord(date: new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified));

        Assert.Equal(expectedValid, result.IsValid);
        if (!expectedValid)
        {
            Assert.Equal("Lütfen geçerli bir tarih seçin.", Assert.Single(result.Errors).ErrorMessage);
        }
    }

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void HealthRecord_NotesLengthBoundary(int length, bool expectedValid)
    {
        var result = ValidateRecord(notes: new string('n', length));

        Assert.Equal(expectedValid, result.IsValid);
        if (!expectedValid)
        {
            Assert.Equal("Notlar en fazla 500 karakter olabilir.", Assert.Single(result.Errors).ErrorMessage);
        }
    }

    [Fact]
    public void HealthRecordInput_DropsTimeAndTurnsBlankNotesIntoNull()
    {
        var input = HealthRecordInput.Create(1, new DateTime(2025, 6, 1, 17, 45, 0, DateTimeKind.Unspecified), 70, 80, "   ");

        Assert.Equal(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Unspecified), input.Date);
        Assert.Null(input.Notes);
        Assert.Equal("not", HealthRecordInput.Create(1, Today, 70, 80, "  not ").Notes);
    }

    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(1, 0, true)]
    [InlineData(1440, 0, true)]
    [InlineData(1441, 0, false)]
    [InlineData(-1, 5, false)]
    [InlineData(0, 1000, true)]
    [InlineData(0, 1001, false)]
    [InlineData(5, -1, false)]
    [InlineData(0, 0, false)]
    public void Exercise_DurationAndRepetitionBoundaries(int duration, int repetitions, bool expectedValid)
    {
        var result = new ExerciseInputValidator().Validate(ExerciseInput.Create(1, "Koşu", duration, repetitions));

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData(1441, 0, "Süre (dakika) 0 ile 1440 arasında olmalıdır.")]
    [InlineData(0, 1001, "Tekrar sayısı 0 ile 1000 arasında olmalıdır.")]
    [InlineData(0, 0, "Süre veya tekrar sayısından en az biri sıfırdan büyük olmalıdır.")]
    public void Exercise_InvalidEffort_HasTurkishMessage(int duration, int repetitions, string expected)
    {
        var result = new ExerciseInputValidator().Validate(ExerciseInput.Create(1, "Koşu", duration, repetitions));

        Assert.Equal(expected, Assert.Single(result.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData(null, "Egzersiz adı alanı boş bırakılamaz.")]
    [InlineData("   ", "Egzersiz adı alanı boş bırakılamaz.")]
    public void Exercise_NameIsRequired(string? name, string expected)
    {
        var result = new ExerciseInputValidator().Validate(ExerciseInput.Create(1, name, 10, 0));

        Assert.Equal(expected, Assert.Single(result.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Exercise_NameLengthBoundary(int length, bool expectedValid)
    {
        var result = new ExerciseInputValidator().Validate(ExerciseInput.Create(1, new string('e', length), 10, 0));

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void Inputs_AreTrimmedAndNullBecomesEmpty()
    {
        Assert.Equal(new UserInput("Ad", 3, string.Empty), UserInput.Create("  Ad ", 3, null));
        Assert.Equal(new ExerciseInput(2, string.Empty, 1, 0), ExerciseInput.Create(2, null, 1, 0));
    }

    private FluentValidation.Results.ValidationResult ValidateRecord(
        DateTime? date = null, decimal weight = 70, int pulse = 80, string? notes = null) =>
        new HealthRecordInputValidator(_time).Validate(HealthRecordInput.Create(1, date ?? Today, weight, pulse, notes));
}

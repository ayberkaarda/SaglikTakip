using SaglikTakip.Domain.Entities;
using SaglikTakip.Domain.Repositories;

namespace SaglikTakip.Business.Services;

public interface IUserService
{
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<User> AddAsync(string? name, int age, string? gender, CancellationToken cancellationToken = default);
}

internal sealed class UserService(IUserRepository users) : IUserService
{
    internal const int MaxNameLength = 100;
    internal const int MinAge = 0;
    internal const int MaxAge = 150;

    public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) =>
        users.GetAllAsync(cancellationToken);

    public async Task<User> AddAsync(string? name, int age, string? gender, CancellationToken cancellationToken = default)
    {
        var user = new User
        {
            Name = Validation.Required(name, "Ad", MaxNameLength),
            Age = Validation.Range(age, "Yaş", MinAge, MaxAge),
            Gender = RequireGender(gender),
        };

        user.Id = await users.AddAsync(user, cancellationToken).ConfigureAwait(false);
        return user;
    }

    private static string RequireGender(string? gender)
    {
        var trimmed = gender?.Trim();
        return Genders.All.FirstOrDefault(g => string.Equals(g, trimmed, StringComparison.Ordinal))
            ?? throw new BusinessRuleException("Lütfen cinsiyet seçin.");
    }
}

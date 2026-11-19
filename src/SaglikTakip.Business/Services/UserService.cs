using FluentValidation;
using SaglikTakip.Business.Validation;
using SaglikTakip.Domain.Entities;
using SaglikTakip.Domain.Repositories;

namespace SaglikTakip.Business.Services;

public interface IUserService
{
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<User> AddAsync(string? name, int age, string? gender, CancellationToken cancellationToken = default);
}

internal sealed class UserService(IUserRepository users, IValidator<UserInput> validator) : IUserService
{
    public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) =>
        users.GetAllAsync(cancellationToken);

    public async Task<User> AddAsync(string? name, int age, string? gender, CancellationToken cancellationToken = default)
    {
        var input = UserInput.Create(name, age, gender);
        validator.ValidateOrThrow(input);

        var user = new User { Name = input.Name, Age = input.Age, Gender = input.Gender };

        user.Id = await users.AddAsync(user, cancellationToken).ConfigureAwait(false);
        return user;
    }
}

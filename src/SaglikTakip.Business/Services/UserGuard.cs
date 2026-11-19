using SaglikTakip.Domain.Repositories;

namespace SaglikTakip.Business.Services;

internal static class UserGuard
{
    public static async Task EnsureExistsAsync(IUserRepository users, int userId, CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            throw new BusinessRuleException("Lütfen bir kullanıcı seçin.");
        }

        if (!await users.ExistsAsync(userId, cancellationToken).ConfigureAwait(false))
        {
            throw new BusinessRuleException("Seçilen kullanıcı bulunamadı.");
        }
    }
}

using Rates.UserService.Domain;

namespace Rates.UserService.Application;

/// <summary>
/// Внутренние порты, необходимые обработчикам UserService. Реализуются
/// в Infrastructure.
/// </summary>
public interface IUserRepository
{
    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<User?> FindByNameAsync(string name, CancellationToken cancellationToken);
    Task AddAsync(User user, CancellationToken cancellationToken);
}

public interface IFavoritesRepository
{
    /// <summary>
    /// Возвращает избранные валюты пользователя вместе с датой добавления. Используется
    /// UI UserService для отображения списка избранного с метаданными.
    /// </summary>
    Task<IReadOnlyList<UserFavoriteSummary>> ListWithAddedAtAsync(Guid userId, CancellationToken cancellationToken);
    Task AddIfMissingAsync(Guid userId, string code, DateTimeOffset now, CancellationToken cancellationToken);
    Task<bool> RemoveAsync(Guid userId, string code, CancellationToken cancellationToken);
}

public sealed record UserFavoriteSummary(string Code, DateTimeOffset AddedAt);

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken);
    Task<RefreshToken?> FindActiveByPlainAsync(string plainToken, CancellationToken cancellationToken);
    Task RevokeAsync(Guid tokenId, DateTimeOffset now, Guid? replacedById, CancellationToken cancellationToken);
}

/// <summary>
/// Read-порт для проверки существования валюты в общей БД. Реализация
/// в Infrastructure читает общий <c>currency</c> через EF Core.
/// </summary>
public interface ICurrencyLookup
{
    Task<bool> ExistsAsync(string code, CancellationToken cancellationToken);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface IAccessTokenService
{
    (string Token, DateTimeOffset ExpiresAt) Issue(Guid userId, string name);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

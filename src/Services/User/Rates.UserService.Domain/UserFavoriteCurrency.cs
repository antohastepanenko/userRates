using Rates.BuildingBlocks.Domain;

namespace Rates.UserService.Domain;

/// <summary>
/// Сущность-связь: какие коды валют пользователь отметил как избранные.
/// </summary>
public sealed class UserFavoriteCurrency : Entity<Guid>
{
    public UserFavoriteCurrency(Guid id, Guid userId, string currencyCode, DateTimeOffset addedAt)
        : base(id)
    {
        UserId = userId;
        CurrencyCode = currencyCode;
        AddedAt = addedAt;
    }

    public Guid UserId { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    public DateTimeOffset AddedAt { get; private set; }

    public static UserFavoriteCurrency Create(Guid userId, string currencyCode, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("userId must be set.", nameof(userId));
        }

        var normalized = (currencyCode ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized.Length != 3)
        {
            throw new ArgumentException("Currency code must be 3 letters long.", nameof(currencyCode));
        }

        return new UserFavoriteCurrency(Guid.NewGuid(), userId, normalized, now);
    }
}
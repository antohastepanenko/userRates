using Rates.BuildingBlocks.Domain;

namespace Rates.UserService.Domain;

/// <summary>
/// Сущность-связь: какие коды валют пользователь отметил как избранные.
/// На уровне БД имеет внешний ключ на <c>currency.code</c> в общей БД,
/// что не даёт сохранить несуществующий код.
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

    /// <summary>Конструктор без параметров для EF Core. Не использовать напрямую.</summary>
    private UserFavoriteCurrency()
        : base(Guid.Empty)
    {
    }

    public Guid UserId { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    public DateTimeOffset AddedAt { get; private set; }

    public static UserFavoriteCurrency Create(Guid userId, string currencyCode, DateTimeOffset addedAt)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("userId must be set.", nameof(userId));
        }

        var normalized = currencyCode.Trim().ToUpperInvariant();
        if (normalized.Length != 3)
        {
            throw new ArgumentException("Currency code must be 3 letters long.", nameof(currencyCode));
        }

        return new UserFavoriteCurrency(Guid.NewGuid(), userId, normalized, addedAt);
    }
}

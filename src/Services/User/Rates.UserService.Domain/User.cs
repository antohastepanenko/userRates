using Rates.BuildingBlocks.Domain;

namespace Rates.UserService.Domain;

/// <summary>
/// Пользователь приложения. Пароли хранятся только в виде BCrypt-хэшей; домен
/// никогда не видит значения в открытом виде.
/// </summary>
public sealed class User : AggregateRoot<Guid>
{
    private User(Guid id, string name, string passwordHash, DateTimeOffset createdAt)
        : base(id)
    {
        Name = name;
        PasswordHash = passwordHash;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    /// <summary>Конструктор без параметров для EF Core. Не использовать напрямую.</summary>
    private User()
        : base(Guid.Empty)
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static User Create(string name, string passwordHash, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return new User(Guid.NewGuid(), name, passwordHash, now);
    }
}

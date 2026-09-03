using Rates.BuildingBlocks.Domain;

namespace Rates.UserService.Domain;

/// <summary>
/// Refresh-токен, выпускаемый auth-флоу. Сохраняется только SHA-256-хэш секрета;
/// значение в открытом виде возвращается вызывающему ровно один раз.
/// </summary>
public sealed class RefreshToken : Entity<Guid>
{
    private RefreshToken(Guid id, Guid userId, string tokenHash, DateTimeOffset expiresAt, DateTimeOffset createdAt)
        : base(id)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
    }

    private RefreshToken()
        : base(Guid.Empty)
    {
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? ReplacedById { get; private set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;

    public static RefreshToken Issue(Guid userId, string tokenHash, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("userId must be set.", nameof(userId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        return new RefreshToken(Guid.NewGuid(), userId, tokenHash, expiresAt, now);
    }

    public void Revoke(DateTimeOffset now, Guid? replacedById = null)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = now;
        ReplacedById = replacedById;
    }
}
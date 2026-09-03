using Microsoft.Extensions.Options;
using Rates.BuildingBlocks.Infrastructure.Options;
using Rates.BuildingBlocks.Infrastructure.Security;
using Rates.UserService.Application;
using Rates.UserService.Domain;

namespace Rates.UserService.Infrastructure.Auth;

/// <summary>
/// Связывает <see cref="IJwtTokenService"/> (определённый в BuildingBlocks.Infrastructure)
/// с контрактом <see cref="IAccessTokenService"/> из UserService.Application.
/// </summary>
public sealed class JwtAccessTokenService : IAccessTokenService
{
    private readonly IJwtTokenService _tokens;

    public JwtAccessTokenService(IJwtTokenService tokens)
    {
        _tokens = tokens;
    }

    public (string Token, DateTimeOffset ExpiresAt) Issue(Guid userId, string name) =>
        _tokens.IssueAccessToken(userId, name);
}

/// <summary>
/// Создаёт непрозрачные refresh-токены. Открытое значение возвращается один раз;
/// сохраняется только его SHA-256-хэш.
/// </summary>
public sealed class RefreshTokenService : IRefreshTokenService
{
    private readonly IOptions<JwtOptions> _options;
    private readonly IClock _clock;

    public RefreshTokenService(IOptions<JwtOptions> options, IClock clock)
    {
        _options = options;
        _clock = clock;
    }

    public (RefreshToken Token, string Plain) Issue(Guid userId)
    {
        var plain = GenerateSecureToken();
        var hash = Hash(plain);
        var expiresAt = _clock.UtcNow.AddDays(_options.Value.RefreshTokenLifetimeDays);
        var token = RefreshToken.Issue(userId, hash, expiresAt, _clock.UtcNow);
        return (token, plain);
    }

    /// <summary>Общий помощник SHA-256, который используют и выпускающий, и EF-репозиторий.</summary>
    public static string Hash(string refreshToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(refreshToken);
        var bytes = System.Text.Encoding.UTF8.GetBytes(refreshToken);
        var hashed = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hashed).ToLowerInvariant();
    }

    private static string GenerateSecureToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
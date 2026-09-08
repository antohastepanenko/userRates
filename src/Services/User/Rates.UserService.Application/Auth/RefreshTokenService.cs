using System.Security.Cryptography;
using System.Text;
using Rates.UserService.Domain;

namespace Rates.UserService.Application.Auth;

public interface IRefreshTokenService
{
    (RefreshToken Token, string Plain) Issue(Guid userId);
}

/// <summary>
/// Выпускает refresh-токены: генерирует криптостойкую случайную строку,
/// отдельно возвращает её в открытом виде, в БД сохраняет только SHA-256-хэш.
/// </summary>
public sealed class RefreshTokenService(IClock clock) : IRefreshTokenService
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    public (RefreshToken Token, string Plain) Issue(Guid userId)
    {
        Span<byte> buffer = stackalloc byte[32];
        RandomNumberGenerator.Fill(buffer);
        var plain = Convert.ToBase64String(buffer).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var hash = Hash(plain);
        var now = clock.UtcNow;
        var token = RefreshToken.Issue(userId, hash, now + Lifetime, now);
        return (token, plain);
    }

    /// <summary>Совместимо с репозиторием, который ищет токен по хэшу.</summary>
    public static string Hash(string plain) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(plain)));
}

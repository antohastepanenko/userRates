namespace Rates.UserService.Application.Auth;

/// <summary>Пара: JWT access-токен + opaque refresh-токен, возвращаемая клиенту.</summary>
public sealed record TokenPair(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt);

public sealed record AuthenticatedUser(
    Guid UserId,
    string Name,
    DateTimeOffset CreatedAt,
    TokenPair Tokens);
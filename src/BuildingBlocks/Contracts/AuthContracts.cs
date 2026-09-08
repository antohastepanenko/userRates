namespace Rates.BuildingBlocks.Contracts;

public sealed record RegisterRequest(string Name, string Password);

public sealed record LoginRequest(string Name, string Password);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt,
    string TokenType = "Bearer");

public sealed record CurrentUserResponse(Guid Id, string Name, DateTimeOffset CreatedAt);

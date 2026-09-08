using Rates.BuildingBlocks.Infrastructure.Security;

namespace Rates.UserService.Infrastructure.Auth;

/// <summary>
/// Связывает <see cref="IJwtTokenService"/> (определённый в BuildingBlocks.Infrastructure)
/// с контрактом <see cref="Rates.UserService.Application.IAccessTokenService"/>
/// из UserService.Application.
/// </summary>
public sealed class JwtAccessTokenService(IJwtTokenService tokens) : Application.IAccessTokenService
{
    public (string Token, DateTimeOffset ExpiresAt) Issue(Guid userId, string name) =>
        tokens.IssueAccessToken(userId, name);
}

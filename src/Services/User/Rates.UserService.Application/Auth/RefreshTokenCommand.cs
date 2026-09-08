using MediatR;
using Rates.BuildingBlocks.Application;
using Rates.BuildingBlocks.Domain;

namespace Rates.UserService.Application.Auth;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<Result<AuthenticatedUser>>;

public sealed class RefreshTokenCommandHandler(
    IRefreshTokenRepository refreshTokens,
    IUserRepository users,
    IAccessTokenService accessTokens,
    IRefreshTokenService refreshTokenFactory,
    IClock clock,
    IUnitOfWorkFactory unitOfWork)
    : IRequestHandler<RefreshTokenCommand, Result<AuthenticatedUser>>
{
    public async Task<Result<AuthenticatedUser>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var existing = await refreshTokens.FindActiveByPlainAsync(request.RefreshToken, cancellationToken);
        if (existing is null)
        {
            return Result<AuthenticatedUser>.Failure(
                Error.Unauthorized("invalid_refresh_token", "Refresh token is invalid, revoked or expired."));
        }

        var user = await users.FindByIdAsync(existing.UserId, cancellationToken);
        if (user is null)
        {
            return Result<AuthenticatedUser>.Failure(
                Error.Unauthorized("user_not_found", "User owning the refresh token no longer exists."));
        }

        var (newRefresh, newRefreshPlain) = refreshTokenFactory.Issue(user.Id);
        existing.Revoke(clock.UtcNow, newRefresh.Id);
        await refreshTokens.AddAsync(newRefresh, cancellationToken);

        var (accessToken, accessExpiresAt) = accessTokens.Issue(user.Id, user.Name);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<AuthenticatedUser>.Ok(new AuthenticatedUser(
            user.Id,
            user.Name,
            user.CreatedAt,
            new TokenPair(accessToken, newRefreshPlain, accessExpiresAt, newRefresh.ExpiresAt)));
    }
}

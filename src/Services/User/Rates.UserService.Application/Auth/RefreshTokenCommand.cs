using FluentValidation;
using MediatR;
using Rates.BuildingBlocks.Domain;

namespace Rates.UserService.Application.Auth;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<Result<AuthenticatedUser>>;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(c => c.RefreshToken).NotEmpty();
    }
}

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<AuthenticatedUser>>
{
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IUserRepository _users;
    private readonly IAccessTokenService _accessTokens;
    private readonly IRefreshTokenService _refreshTokenFactory;
    private readonly IClock _clock;
    private readonly IUnitOfWorkFactory _unitOfWork;

    public RefreshTokenCommandHandler(
        IRefreshTokenRepository refreshTokens,
        IUserRepository users,
        IAccessTokenService accessTokens,
        IRefreshTokenService refreshTokenFactory,
        IClock clock,
        IUnitOfWorkFactory unitOfWork)
    {
        _refreshTokens = refreshTokens;
        _users = users;
        _accessTokens = accessTokens;
        _refreshTokenFactory = refreshTokenFactory;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AuthenticatedUser>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var existing = await _refreshTokens.FindActiveByPlainAsync(request.RefreshToken, cancellationToken);
        if (existing is null)
        {
            return Result<AuthenticatedUser>.Failure(
                Error.Unauthorized("invalid_refresh_token", "Refresh token is invalid, revoked or expired."));
        }

        var user = await _users.FindByIdAsync(existing.UserId, cancellationToken);
        if (user is null)
        {
            return Result<AuthenticatedUser>.Failure(
                Error.Unauthorized("user_not_found", "User owning the refresh token no longer exists."));
        }

        var (newRefresh, newRefreshPlain) = _refreshTokenFactory.Issue(user.Id);
        existing.Revoke(_clock.UtcNow, newRefresh.Id);
        await _refreshTokens.AddAsync(newRefresh, cancellationToken);

        var (accessToken, accessExpiresAt) = _accessTokens.Issue(user.Id, user.Name);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<AuthenticatedUser>.Ok(new AuthenticatedUser(
            user.Id,
            user.Name,
            user.CreatedAt,
            new TokenPair(accessToken, newRefreshPlain, accessExpiresAt, newRefresh.ExpiresAt)));
    }
}
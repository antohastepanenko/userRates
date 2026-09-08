using MediatR;
using Rates.BuildingBlocks.Application;
using Rates.BuildingBlocks.Domain;

namespace Rates.UserService.Application.Auth;

public sealed record LoginUserCommand(string Name, string Password) : ICommand<Result<AuthenticatedUser>>;

public sealed class LoginUserCommandHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    IAccessTokenService accessTokens,
    IRefreshTokenService refreshTokenFactory,
    IUnitOfWorkFactory unitOfWork)
    : IRequestHandler<LoginUserCommand, Result<AuthenticatedUser>>
{
    public async Task<Result<AuthenticatedUser>> Handle(LoginUserCommand request, CancellationToken cancellationToken)
    {
        var user = await users.FindByNameAsync(request.Name.Trim(), cancellationToken);
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            // Общее сообщение — не раскрываем, существует ли пользователь.
            return Result<AuthenticatedUser>.Failure(
                Error.Unauthorized("invalid_credentials", "Invalid name or password."));
        }

        var (refreshToken, refreshPlain) = refreshTokenFactory.Issue(user.Id);
        await refreshTokens.AddAsync(refreshToken, cancellationToken);

        var (accessToken, accessExpiresAt) = accessTokens.Issue(user.Id, user.Name);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<AuthenticatedUser>.Ok(new AuthenticatedUser(
            user.Id,
            user.Name,
            user.CreatedAt,
            new TokenPair(accessToken, refreshPlain, accessExpiresAt, refreshToken.ExpiresAt)));
    }
}

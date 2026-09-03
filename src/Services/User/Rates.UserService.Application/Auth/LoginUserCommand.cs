using FluentValidation;
using MediatR;
using Rates.BuildingBlocks.Domain;

namespace Rates.UserService.Application.Auth;

public sealed record LoginUserCommand(string Name, string Password) : ICommand<Result<AuthenticatedUser>>;

public sealed class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty();
        RuleFor(c => c.Password).NotEmpty();
    }
}

public sealed class LoginUserCommandHandler : IRequestHandler<LoginUserCommand, Result<AuthenticatedUser>>
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAccessTokenService _accessTokens;
    private readonly IRefreshTokenService _refreshTokenFactory;
    private readonly IClock _clock;
    private readonly IUnitOfWorkFactory _unitOfWork;

    public LoginUserCommandHandler(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher passwordHasher,
        IAccessTokenService accessTokens,
        IRefreshTokenService refreshTokenFactory,
        IClock clock,
        IUnitOfWorkFactory unitOfWork)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _passwordHasher = passwordHasher;
        _accessTokens = accessTokens;
        _refreshTokenFactory = refreshTokenFactory;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AuthenticatedUser>> Handle(LoginUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.FindByNameAsync(request.Name.Trim(), cancellationToken);
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            // Общее сообщение — не раскрываем, существует ли пользователь.
            return Result<AuthenticatedUser>.Failure(
                Error.Unauthorized("invalid_credentials", "Invalid name or password."));
        }

        var (refreshToken, refreshPlain) = _refreshTokenFactory.Issue(user.Id);
        await _refreshTokens.AddAsync(refreshToken, cancellationToken);

        var (accessToken, accessExpiresAt) = _accessTokens.Issue(user.Id, user.Name);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<AuthenticatedUser>.Ok(new AuthenticatedUser(
            user.Id,
            user.Name,
            user.CreatedAt,
            new TokenPair(accessToken, refreshPlain, accessExpiresAt, refreshToken.ExpiresAt)));
    }
}
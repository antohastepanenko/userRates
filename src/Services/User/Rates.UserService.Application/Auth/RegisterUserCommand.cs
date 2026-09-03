using FluentValidation;
using MediatR;
using Rates.BuildingBlocks.Domain;
using Rates.UserService.Domain;

namespace Rates.UserService.Application.Auth;

/// <summary>Регистрация нового пользователя. Возвращает только что созданного пользователя и начальную пару токенов.</summary>
public sealed record RegisterUserCommand(string Name, string Password) : ICommand<Result<AuthenticatedUser>>;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty()
            .Length(3, 100)
            .Matches("^[A-Za-z0-9_.-]+$")
            .WithMessage("Name must be 3-100 characters, letters/digits/underscore/dot/dash only.");

        RuleFor(c => c.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128);
    }
}

public sealed class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, Result<AuthenticatedUser>>
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAccessTokenService _accessTokens;
    private readonly IRefreshTokenService _refreshTokenFactory;
    private readonly IClock _clock;
    private readonly IUnitOfWorkFactory _unitOfWork;

    public RegisterUserCommandHandler(
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

    public async Task<Result<AuthenticatedUser>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        var existing = await _users.FindByNameAsync(name, cancellationToken);
        if (existing is not null)
        {
            return Result<AuthenticatedUser>.Failure(
                Error.Conflict("user_already_exists", "A user with this name already exists."));
        }

        var hash = _passwordHasher.Hash(request.Password);
        var user = User.Create(name, hash, _clock.UtcNow);
        await _users.AddAsync(user, cancellationToken);

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
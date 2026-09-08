using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Rates.BuildingBlocks.Application;
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

public sealed class RegisterUserCommandHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    IAccessTokenService accessTokens,
    IRefreshTokenService refreshTokenFactory,
    IClock clock,
    IUnitOfWorkFactory unitOfWork)
    : IRequestHandler<RegisterUserCommand, Result<AuthenticatedUser>>
{
    public async Task<Result<AuthenticatedUser>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        var hash = passwordHasher.Hash(request.Password);
        var user = User.Create(name, hash, clock.UtcNow);
        await users.AddAsync(user, cancellationToken);

        var (refreshToken, refreshPlain) = refreshTokenFactory.Issue(user.Id);
        await refreshTokens.AddAsync(refreshToken, cancellationToken);

        var (accessToken, accessExpiresAt) = accessTokens.Issue(user.Id, user.Name);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Гонка регистрации: между FindByNameAsync и SaveChangesAsync другой запрос
            // занял это имя. Возвращаем 409 Conflict, а не 500.
            return Result<AuthenticatedUser>.Failure(
                Error.Conflict("user_already_exists", "A user with this name already exists."));
        }

        return Result<AuthenticatedUser>.Ok(new AuthenticatedUser(
            user.Id,
            user.Name,
            user.CreatedAt,
            new TokenPair(accessToken, refreshPlain, accessExpiresAt, refreshToken.ExpiresAt)));
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is Npgsql.PostgresException { SqlState: "23505" };
}

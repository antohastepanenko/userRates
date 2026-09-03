using FluentValidation;
using MediatR;
using Rates.BuildingBlocks.Domain;

namespace Rates.UserService.Application.Auth;

public sealed record LogoutCommand(string RefreshToken) : ICommand<Result<Unit>>;

public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(c => c.RefreshToken).NotEmpty();
    }
}

public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result<Unit>>
{
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IClock _clock;
    private readonly IUnitOfWorkFactory _unitOfWork;

    public LogoutCommandHandler(
        IRefreshTokenRepository refreshTokens,
        IClock clock,
        IUnitOfWorkFactory unitOfWork)
    {
        _refreshTokens = refreshTokens;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Unit>> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var existing = await _refreshTokens.FindActiveByPlainAsync(request.RefreshToken, cancellationToken);
        if (existing is null)
        {
            // Выход из системы с уже отозванным токеном — успех без каких-либо действий.
            return Result<Unit>.Ok(Unit.Value);
        }

        existing.Revoke(_clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Unit>.Ok(Unit.Value);
    }
}
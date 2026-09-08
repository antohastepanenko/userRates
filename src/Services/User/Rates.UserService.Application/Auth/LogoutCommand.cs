using MediatR;
using Rates.BuildingBlocks.Application;
using Rates.BuildingBlocks.Domain;

namespace Rates.UserService.Application.Auth;

public sealed record LogoutCommand(string RefreshToken) : ICommand<Result<Unit>>;

public sealed class LogoutCommandHandler(
    IRefreshTokenRepository refreshTokens,
    IClock clock,
    IUnitOfWorkFactory unitOfWork)
    : IRequestHandler<LogoutCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var existing = await refreshTokens.FindActiveByPlainAsync(request.RefreshToken, cancellationToken);
        if (existing is null)
        {
            // Выход из системы с уже отозванным токеном — успех без каких-либо действий.
            return Result<Unit>.Ok(Unit.Value);
        }

        existing.Revoke(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Unit>.Ok(Unit.Value);
    }
}

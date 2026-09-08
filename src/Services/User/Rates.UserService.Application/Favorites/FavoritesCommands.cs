using MediatR;
using Microsoft.EntityFrameworkCore;
using Rates.BuildingBlocks.Application;
using Rates.BuildingBlocks.Domain;
namespace Rates.UserService.Application.Favorites;

public sealed record FavoriteCurrencyView(string Code, DateTimeOffset AddedAt);

public sealed record GetFavoriteCurrenciesQuery : IQuery<Result<IReadOnlyList<FavoriteCurrencyView>>>;

public sealed class GetFavoriteCurrenciesQueryHandler(ICurrentUser currentUser, IFavoritesRepository favorites)
    : IRequestHandler<GetFavoriteCurrenciesQuery, Result<IReadOnlyList<FavoriteCurrencyView>>>
{
    public async Task<Result<IReadOnlyList<FavoriteCurrencyView>>> Handle(GetFavoriteCurrenciesQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        {
            return Result<IReadOnlyList<FavoriteCurrencyView>>.Failure(
                Error.Unauthorized("not_authenticated", "Authentication is required."));
        }

        var userFavorites = await favorites.ListWithAddedAtAsync(currentUser.UserId.Value, cancellationToken);
        var view = userFavorites
            .Select(f => new FavoriteCurrencyView(f.Code, f.AddedAt))
            .ToArray();
        return Result<IReadOnlyList<FavoriteCurrencyView>>.Ok(view);
    }
}

public sealed record AddFavoriteCurrencyCommand(string Code) : ICommand<Result<Unit>>;

public sealed class AddFavoriteCurrencyCommandHandler(
    ICurrentUser currentUser,
    IFavoritesRepository favorites,
    ICurrencyLookup currencyLookup,
    IClock clock,
    IUnitOfWorkFactory unitOfWork)
    : IRequestHandler<AddFavoriteCurrencyCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(AddFavoriteCurrencyCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        {
            return Result<Unit>.Failure(Error.Unauthorized("not_authenticated", "Authentication is required."));
        }

        var normalized = request.Code.Trim().ToUpperInvariant();
        if (normalized.Length != 3)
        {
            return Result<Unit>.Failure(Error.Validation("invalid_currency_code", "Currency code must be 3 letters."));
        }

        if (!await currencyLookup.ExistsAsync(normalized, cancellationToken))
        {
            return Result<Unit>.Failure(Error.Validation(
                "unknown_currency",
                $"Currency '{normalized}' is not supported by the platform."));
        }

        await favorites.AddIfMissingAsync(currentUser.UserId.Value, normalized, clock.UtcNow, cancellationToken);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Дубликат (user_id, currency_code) — это идемпотентный успех.
            return Result<Unit>.Ok(Unit.Value);
        }

        return Result<Unit>.Ok(Unit.Value);
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is Npgsql.PostgresException { SqlState: "23505" };
}

public sealed record RemoveFavoriteCurrencyCommand(string Code) : ICommand<Result<bool>>;

public sealed class RemoveFavoriteCurrencyCommandHandler(
    ICurrentUser currentUser,
    IFavoritesRepository favorites,
    IUnitOfWorkFactory unitOfWork)
    : IRequestHandler<RemoveFavoriteCurrencyCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(RemoveFavoriteCurrencyCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        {
            return Result<bool>.Failure(Error.Unauthorized("not_authenticated", "Authentication is required."));
        }

        var normalized = request.Code.Trim().ToUpperInvariant();
        if (normalized.Length != 3)
        {
            return Result<bool>.Failure(Error.Validation("invalid_currency_code", "Currency code must be 3 letters."));
        }

        var removed = await favorites.RemoveAsync(currentUser.UserId.Value, normalized, cancellationToken);
        if (removed)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result<bool>.Ok(removed);
    }
}

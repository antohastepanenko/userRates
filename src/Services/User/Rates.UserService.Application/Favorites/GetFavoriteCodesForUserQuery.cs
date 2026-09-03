using MediatR;
using Rates.BuildingBlocks.Domain;

namespace Rates.UserService.Application.Favorites;

/// <summary>
/// Возвращает только коды избранных валют для указанного пользователя. Используется
/// Finance-сервисом через внутренний HTTP-эндпоинт; JWT не требуется, так как вызывающий —
/// это сам доверенный finance-api.
/// </summary>
public sealed record GetFavoriteCodesForUserQuery(Guid UserId) : IQuery<Result<IReadOnlyList<string>>>;

public sealed class GetFavoriteCodesForUserQueryHandler : IRequestHandler<GetFavoriteCodesForUserQuery, Result<IReadOnlyList<string>>>
{
    private readonly IFavoritesRepository _favorites;
    private readonly IUserRepository _users;

    public GetFavoriteCodesForUserQueryHandler(IFavoritesRepository favorites, IUserRepository users)
    {
        _favorites = favorites;
        _users = users;
    }

    public async Task<Result<IReadOnlyList<string>>> Handle(GetFavoriteCodesForUserQuery request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
        {
            return Result<IReadOnlyList<string>>.Failure(
                Error.Validation("invalid_user_id", "userId must be supplied."));
        }

        var user = await _users.FindByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return Result<IReadOnlyList<string>>.Failure(
                Error.NotFound("user_not_found", "User does not exist."));
        }

        var codes = await _favorites.ListCodesAsync(request.UserId, cancellationToken);
        return Result<IReadOnlyList<string>>.Ok(codes);
    }
}
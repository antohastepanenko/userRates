using MediatR;
using Rates.BuildingBlocks.Domain;

namespace Rates.UserService.Application.Favorites;

/// <summary>
/// Возвращает избранные валюты указанного пользователя вместе с датой добавления.
/// Используется Finance-сервисом через внутренний HTTP-эндпоинт; JWT не требуется,
/// так как вызывающий — сам доверенный finance-api, аутентифицированный
/// <c>X-Internal-Service-Token</c>.
/// </summary>
public sealed record GetFavoriteCodesForUserQuery(Guid UserId)
    : IQuery<Result<IReadOnlyList<UserFavoriteSummary>>>;

public sealed class GetFavoriteCodesForUserQueryHandler
    : IRequestHandler<GetFavoriteCodesForUserQuery, Result<IReadOnlyList<UserFavoriteSummary>>>
{
    private readonly IFavoritesRepository _favorites;
    private readonly IUserRepository _users;

    public GetFavoriteCodesForUserQueryHandler(IFavoritesRepository favorites, IUserRepository users)
    {
        _favorites = favorites;
        _users = users;
    }

    public async Task<Result<IReadOnlyList<UserFavoriteSummary>>> Handle(
        GetFavoriteCodesForUserQuery request,
        CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
        {
            return Result<IReadOnlyList<UserFavoriteSummary>>.Failure(
                Error.Validation("invalid_user_id", "userId must be supplied."));
        }

        var user = await _users.FindByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return Result<IReadOnlyList<UserFavoriteSummary>>.Failure(
                Error.NotFound("user_not_found", "User does not exist."));
        }

        var items = await _favorites.ListWithAddedAtAsync(request.UserId, cancellationToken);
        return Result<IReadOnlyList<UserFavoriteSummary>>.Ok(items);
    }
}
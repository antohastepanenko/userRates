using Rates.BuildingBlocks.Domain;

namespace Rates.FinanceService.Application.Users;

/// <summary>
/// Порт application-слоя для чтения из UserService кодов избранных валют пользователя.
/// Транспорт и межсервисная аутентификация остаются в Infrastructure.
/// </summary>
public interface IUserFavoritesClient
{
    Task<Result<IReadOnlyList<string>>> GetFavoriteCodesAsync(
        Guid userId,
        CancellationToken cancellationToken);
}

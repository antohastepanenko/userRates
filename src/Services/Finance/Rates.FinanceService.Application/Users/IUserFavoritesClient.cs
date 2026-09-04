using Rates.BuildingBlocks.Domain;

namespace Rates.FinanceService.Application.Users;

/// <summary>
/// Запись об избранной валюте пользователя, полученная из UserService. Содержит код
/// и время добавления, чтобы Finance-слой мог показать в UI не только курс, но и
/// «когда пользователь добавил эту валюту в избранное».
/// </summary>
public sealed record FavoriteEntry(string Code, DateTimeOffset AddedAt);

/// <summary>
/// Порт application-слоя для чтения из UserService избранных валют пользователя.
/// Транспорт и межсервисная аутентификация остаются в Infrastructure.
/// </summary>
public interface IUserFavoritesClient
{
    Task<Result<IReadOnlyList<FavoriteEntry>>> GetFavoritesAsync(
        Guid userId,
        CancellationToken cancellationToken);
}

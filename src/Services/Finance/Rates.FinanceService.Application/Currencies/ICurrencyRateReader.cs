using Rates.FinanceService.Domain;

namespace Rates.FinanceService.Application.Currencies;

/// <summary>
/// Read-порт над каталогом валют FinanceService.
/// </summary>
public interface ICurrencyRateReader
{
    /// <summary>
    /// Возвращает по одной самой свежей записи курса на каждый <see cref="Currency.Code"/>,
    /// упорядоченные по коду. Используется для построения полного каталога валют, доступных UI.
    /// </summary>
    Task<IReadOnlyList<Currency>> ListLatestAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Снимок курса валюты, агрегированный с записью избранного. Используется в JOIN
/// для возврата последнего известного курса рядом с кодом, добавленным в избранное.
/// </summary>
public sealed record CurrencyRateSnapshot(
    string Code,
    string Name,
    decimal Rate,
    decimal Nominal,
    DateOnly RateDate);

/// <summary>
/// Пара «код избранного + снимок курса». Если <see cref="Rate"/> равен null,
/// каталог не содержит курса для этого кода (или кода нет вовсе — но это уже
/// противоречит FK в БД).
/// </summary>
public sealed record UserFavoriteWithRate(
    string Code,
    DateTimeOffset AddedAt,
    CurrencyRateSnapshot? Rate);

/// <summary>
/// Read-порт для избранного пользователя с уже присоединённым курсом. Реализация
/// FinanceService.Infrastructure делает JOIN через общий контекст; HTTP к UserService
/// больше не требуется.
/// </summary>
public interface IFavoritesLookup
{
    Task<IReadOnlyList<UserFavoriteWithRate>> ListByUserAsync(
        Guid userId,
        CancellationToken cancellationToken);
}

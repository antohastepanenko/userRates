using MediatR;
using Rates.BuildingBlocks.Application;
using Rates.BuildingBlocks.Contracts;
using Rates.BuildingBlocks.Domain;
using Rates.FinanceService.Application.Users;

namespace Rates.FinanceService.Application.Currencies;

/// <summary>
/// Возвращает избранные валюты текущего пользователя с их последними известными курсами
/// и датой добавления в избранное. User id намеренно не включается в запрос — он всегда
/// берётся из JWT.
/// </summary>
public sealed record GetUserCurrencyRatesQuery : IQuery<Result<UserCurrencyRatesView>>;

public sealed record UserCurrencyRatesView(
    DateOnly? AsOf,
    IReadOnlyList<CurrencyRateDto> Items,
    IReadOnlyList<string> MissingCodes);

public sealed class GetUserCurrencyRatesQueryHandler
    : IRequestHandler<GetUserCurrencyRatesQuery, Result<UserCurrencyRatesView>>
{
    /// <summary>
    /// Количество знаков после запятой для UI. ЦБ РФ публикует до 4 знаков; пользователю
    /// этого достаточно, чтобы видеть изменения, но без визуального шума.
    /// </summary>
    internal const int DisplayRateDecimals = 4;

    private readonly ICurrentUser _currentUser;
    private readonly IUserFavoritesClient _favoritesClient;
    private readonly ICurrencyRateReader _currencyRates;

    public GetUserCurrencyRatesQueryHandler(
        ICurrentUser currentUser,
        IUserFavoritesClient favoritesClient,
        ICurrencyRateReader currencyRates)
    {
        _currentUser = currentUser;
        _favoritesClient = favoritesClient;
        _currencyRates = currencyRates;
    }

    public async Task<Result<UserCurrencyRatesView>> Handle(
        GetUserCurrencyRatesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
        {
            return Result<UserCurrencyRatesView>.Failure(
                Error.Unauthorized("not_authenticated", "Authentication is required."));
        }

        var favoritesResult = await _favoritesClient.GetFavoritesAsync(
            _currentUser.UserId.Value,
            cancellationToken);
        if (favoritesResult.IsFailure)
        {
            return Result<UserCurrencyRatesView>.Failure(favoritesResult.Error);
        }

        var orderedFavorites = favoritesResult.Value
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Code))
            .Select(entry => new FavoriteEntry(entry.Code.Trim().ToUpperInvariant(), entry.AddedAt))
            .DistinctBy(entry => entry.Code, StringComparer.Ordinal)
            .OrderBy(entry => entry.Code, StringComparer.Ordinal)
            .ToArray();

        if (orderedFavorites.Length == 0)
        {
            return Result<UserCurrencyRatesView>.Ok(
                new UserCurrencyRatesView(null, Array.Empty<CurrencyRateDto>(), Array.Empty<string>()));
        }

        var codes = orderedFavorites.Select(entry => entry.Code).ToArray();
        var addedAtByCode = orderedFavorites.ToDictionary(entry => entry.Code, entry => entry.AddedAt, StringComparer.Ordinal);

        var currencies = await _currencyRates.ListByCodesAsync(codes, cancellationToken);
        var currencyByCode = currencies.ToDictionary(currency => currency.Code, StringComparer.Ordinal);

        var items = orderedFavorites
            .Where(entry => currencyByCode.ContainsKey(entry.Code))
            .Select(entry =>
            {
                var currency = currencyByCode[entry.Code];
                return new CurrencyRateDto(
                    currency.Code,
                    currency.Name,
                    Math.Round(currency.Rate, DisplayRateDecimals, MidpointRounding.AwayFromZero),
                    currency.Nominal,
                    currency.RateDate,
                    addedAtByCode[entry.Code]);
            })
            .ToArray();

        var missingCodes = orderedFavorites
            .Where(entry => !currencyByCode.ContainsKey(entry.Code))
            .Select(entry => entry.Code)
            .ToArray();

        DateOnly? asOf = items.Length == 0
            ? (DateOnly?)null
            : items.Max(item => item.RateDate);

        return Result<UserCurrencyRatesView>.Ok(
            new UserCurrencyRatesView(asOf, items, missingCodes));
    }
}

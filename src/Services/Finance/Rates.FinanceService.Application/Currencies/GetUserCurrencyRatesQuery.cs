using MediatR;
using Rates.BuildingBlocks.Application;
using Rates.BuildingBlocks.Contracts;
using Rates.BuildingBlocks.Domain;
using Rates.FinanceService.Application.Users;

namespace Rates.FinanceService.Application.Currencies;

/// <summary>
/// Возвращает избранные валюты текущего пользователя с их последними известными курсами.
/// User id намеренно не включается в запрос — он всегда берётся из JWT.
/// </summary>
public sealed record GetUserCurrencyRatesQuery : IQuery<Result<UserCurrencyRatesView>>;

public sealed record UserCurrencyRatesView(
    DateOnly? AsOf,
    IReadOnlyList<CurrencyRateDto> Items,
    IReadOnlyList<string> MissingCodes);

public sealed class GetUserCurrencyRatesQueryHandler
    : IRequestHandler<GetUserCurrencyRatesQuery, Result<UserCurrencyRatesView>>
{
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

        var favoriteResult = await _favoritesClient.GetFavoriteCodesAsync(
            _currentUser.UserId.Value,
            cancellationToken);
        if (favoriteResult.IsFailure)
        {
            return Result<UserCurrencyRatesView>.Failure(favoriteResult.Error);
        }

        var codes = favoriteResult.Value
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        if (codes.Length == 0)
        {
            return Result<UserCurrencyRatesView>.Ok(
                new UserCurrencyRatesView(null, Array.Empty<CurrencyRateDto>(), Array.Empty<string>()));
        }

        var currencies = await _currencyRates.ListByCodesAsync(codes, cancellationToken);
        var currencyByCode = currencies.ToDictionary(currency => currency.Code, StringComparer.Ordinal);

        var items = codes
            .Where(currencyByCode.ContainsKey)
            .Select(code => currencyByCode[code])
            .OrderBy(currency => currency.Code, StringComparer.Ordinal)
            .Select(currency => new CurrencyRateDto(
                currency.Code,
                currency.Name,
                currency.Rate,
                currency.Nominal,
                currency.RateDate))
            .ToArray();

        var missingCodes = codes
            .Where(code => !currencyByCode.ContainsKey(code))
            .ToArray();

        DateOnly? asOf = items.Length == 0
            ? (DateOnly?)null
            : items.Max(item => item.RateDate);

        return Result<UserCurrencyRatesView>.Ok(
            new UserCurrencyRatesView(asOf, items, missingCodes));
    }
}

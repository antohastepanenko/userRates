using MediatR;
using Rates.BuildingBlocks.Contracts;
using Rates.BuildingBlocks.Domain;

namespace Rates.FinanceService.Application.Currencies;

/// <summary>
/// Возвращает полный каталог валют с последним известным курсом по каждой из них.
/// Не использует избранное — эндпоинт предназначен для построения справочника в UI.
/// </summary>
public sealed record GetLatestCurrencyRatesQuery : IQuery<Result<AllCurrencyRatesView>>;

public sealed record AllCurrencyRatesView(
    DateOnly? AsOf,
    IReadOnlyList<CurrencyRateDto> Items);

public sealed class GetLatestCurrencyRatesQueryHandler
    : IRequestHandler<GetLatestCurrencyRatesQuery, Result<AllCurrencyRatesView>>
{
    private readonly ICurrencyRateReader _currencyRates;

    public GetLatestCurrencyRatesQueryHandler(ICurrencyRateReader currencyRates)
    {
        _currencyRates = currencyRates ?? throw new ArgumentNullException(nameof(currencyRates));
    }

    public async Task<Result<AllCurrencyRatesView>> Handle(
        GetLatestCurrencyRatesQuery request,
        CancellationToken cancellationToken)
    {
        var currencies = await _currencyRates.ListLatestAsync(cancellationToken);

        var items = currencies
            .OrderBy(currency => currency.Code, StringComparer.Ordinal)
            .Select(currency => new CurrencyRateDto(
                currency.Code,
                currency.Name,
                Math.Round(currency.Rate, GetUserCurrencyRatesQueryHandler.DisplayRateDecimals, MidpointRounding.AwayFromZero),
                currency.Nominal,
                currency.RateDate,
                AddedAt: null))
            .ToArray();

        DateOnly? asOf = items.Length == 0
            ? null
            : items.Max(item => item.RateDate);

        return Result<AllCurrencyRatesView>.Ok(new AllCurrencyRatesView(asOf, items));
    }
}

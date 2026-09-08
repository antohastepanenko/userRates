using MediatR;
using Rates.BuildingBlocks.Application;
using Rates.BuildingBlocks.Contracts;
using Rates.BuildingBlocks.Domain;

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

public sealed class GetUserCurrencyRatesQueryHandler(
    ICurrentUser currentUser,
    IFavoritesLookup favorites) : IRequestHandler<GetUserCurrencyRatesQuery, Result<UserCurrencyRatesView>>
{
    /// <summary>
    /// Количество знаков после запятой для UI. ЦБ РФ публикует до 4 знаков; пользователю
    /// этого достаточно, чтобы видеть изменения, но без визуального шума.
    /// </summary>
    internal const int DisplayRateDecimals = 4;

    public async Task<Result<UserCurrencyRatesView>> Handle(
        GetUserCurrencyRatesQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        {
            return Result<UserCurrencyRatesView>.Failure(
                Error.Unauthorized("not_authenticated", "Authentication is required."));
        }

        var entries = await favorites.ListByUserAsync(currentUser.UserId.Value, cancellationToken);
        if (entries.Count == 0)
        {
            return Result<UserCurrencyRatesView>.Ok(
                new UserCurrencyRatesView(null, Array.Empty<CurrencyRateDto>(), Array.Empty<string>()));
        }

        var items = new List<CurrencyRateDto>(entries.Count);
        var missingCodes = new List<string>();

        foreach (var entry in entries)
        {
            if (entry.Rate is null)
            {
                // Запись избранного существует, но курс отсутствует (currency без каталога либо
                // запись старше каталога). Не должно происходить благодаря FK, но обрабатывается
                // на всякий случай.
                missingCodes.Add(entry.Code);
                continue;
            }

            var rate = entry.Rate;
            items.Add(new CurrencyRateDto(
                rate.Code,
                rate.Name,
                Math.Round(rate.Rate, DisplayRateDecimals, MidpointRounding.AwayFromZero),
                rate.Nominal,
                rate.RateDate,
                entry.AddedAt));
        }

        DateOnly? asOf = items.Count == 0
            ? null
            : items.Max(item => item.RateDate);

        return Result<UserCurrencyRatesView>.Ok(
            new UserCurrencyRatesView(asOf, items, missingCodes));
    }
}

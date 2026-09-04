using Rates.FinanceService.Domain;

namespace Rates.FinanceService.Application.Currencies;

/// <summary>
/// Read-порт поверх каталога валют FinanceService.
/// </summary>
public interface ICurrencyRateReader
{
    Task<IReadOnlyList<Currency>> ListByCodesAsync(
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken);

    /// <summary>
    /// Возвращает по одной самой свежей записи курса на каждый <see cref="Currency.Code"/>,
    /// упорядоченные по коду. Используется для построения полного каталога валют, доступных UI.
    /// </summary>
    Task<IReadOnlyList<Currency>> ListLatestAsync(CancellationToken cancellationToken);
}

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
}

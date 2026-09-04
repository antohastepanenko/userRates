using Rates.FinanceService.Application.Currencies;
using Rates.FinanceService.Domain;

namespace Rates.FinanceService.Infrastructure.Persistence;

/// <summary>
/// Адаптер read-порта application-слоя поверх EF-репозитория FinanceService.
/// </summary>
public sealed class CurrencyRateReader : ICurrencyRateReader
{
    private readonly CurrencyRepository _repository;

    public CurrencyRateReader(CurrencyRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<IReadOnlyList<Currency>> ListByCodesAsync(
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken)
    {
        return await _repository.ListByCodesAsync(codes, cancellationToken);
    }

    public async Task<IReadOnlyList<Currency>> ListLatestAsync(CancellationToken cancellationToken)
    {
        return await _repository.ListLatestAsync(cancellationToken);
    }
}

using MediatR;
using Rates.BuildingBlocks.Domain;

namespace Rates.FinanceService.Application.Cbr;

/// <summary>
/// Выполняет upsert переданных записей ЦБ в таблицу <c>currency</c>. Worker вызывает
/// эту команду после разбора ежедневной XML-выгрузки.
/// </summary>
public sealed record SyncCurrencyRatesCommand(CbrDailyRates Rates) : ICommand<Result<SyncCurrencyRatesResult>>;

public sealed record SyncCurrencyRatesResult(int UpsertedCount, DateOnly RateDate);

public sealed class SyncCurrencyRatesCommandHandler : IRequestHandler<SyncCurrencyRatesCommand, Result<SyncCurrencyRatesResult>>
{
    private readonly ICurrencySyncRepository _repository;

    public SyncCurrencyRatesCommandHandler(ICurrencySyncRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<Result<SyncCurrencyRatesResult>> Handle(SyncCurrencyRatesCommand request, CancellationToken cancellationToken)
    {
        if (request.Rates is null || request.Rates.Entries.Count == 0)
        {
            return Result<SyncCurrencyRatesResult>.Failure(
                Error.Validation("no_rates", "Nothing to sync: rates payload is empty."));
        }

        var now = DateTimeOffset.UtcNow;
        var count = await _repository.UpsertManyAsync(request.Rates.Entries, request.Rates.RateDate, now, cancellationToken);
        return Result<SyncCurrencyRatesResult>.Ok(new SyncCurrencyRatesResult(count, request.Rates.RateDate));
    }
}

/// <summary>
/// Контракт write-репозитория, используемого обработчиком синхронизации. Реализация
/// находится в Infrastructure (EF Core) и выполняет реальный upsert в таблицу <c>currency</c>.
/// </summary>
public interface ICurrencySyncRepository
{
    Task<int> UpsertManyAsync(IReadOnlyList<CbrCurrencyEntry> entries, DateOnly rateDate, DateTimeOffset now, CancellationToken cancellationToken);
}
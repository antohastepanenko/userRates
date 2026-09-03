using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rates.FinanceService.Application.Cbr;
using Rates.FinanceService.Domain;
using Rates.FinanceService.Infrastructure.Persistence;

namespace Rates.FinanceService.Infrastructure.Cbr;

/// <summary>
/// Реализация <see cref="ICurrencySyncRepository"/> на базе EF Core. Использует транзакцию,
/// чтобы процесс не оставлял таблицу частично заполненной при сбое во время синхронизации.
/// </summary>
public sealed class CurrencySyncRepository : ICurrencySyncRepository
{
    private readonly FinanceDbContext _db;
    private readonly ILogger<CurrencySyncRepository> _logger;

    public CurrencySyncRepository(FinanceDbContext db, ILogger<CurrencySyncRepository> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger;
    }

    public async Task<int> UpsertManyAsync(
        IReadOnlyList<CbrCurrencyEntry> entries,
        DateOnly rateDate,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0)
        {
            return 0;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var existingByCode = await _db.Currencies
            .Where(c => entries.Select(e => e.CharCode).Contains(c.Code))
            .ToDictionaryAsync(c => c.Code, cancellationToken);

        var upserted = 0;
        foreach (var entry in entries)
        {
            var code = entry.CharCode.ToUpperInvariant();
            if (existingByCode.TryGetValue(code, out var existing))
            {
                existing.Update(entry.NormalizedRate, entry.Nominal, rateDate, now);
            }
            else
            {
                var newCurrency = Currency.Upsert(
                    code: entry.CharCode,
                    name: entry.Name,
                    rate: entry.NormalizedRate,
                    nominal: entry.Nominal,
                    rateDate: rateDate,
                    now: now);
                await _db.Currencies.AddAsync(newCurrency, cancellationToken);
            }

            upserted++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation("Upserted {Count} currency entries for {RateDate}", upserted, rateDate);
        return upserted;
    }
}
using Microsoft.EntityFrameworkCore;
using Rates.BuildingBlocks.Persistence;
using Rates.FinanceService.Application.Cbr;
using Rates.FinanceService.Application.Currencies;
using Rates.FinanceService.Domain;

namespace Rates.FinanceService.Infrastructure.Persistence;

/// <summary>
/// Репозиторий каталога валют FinanceService. Инкапсулирует write-операции
/// (upsert через <see cref="ICurrencySyncRepository"/>) и read-операции
/// (<see cref="ICurrencyRateReader"/>) поверх общего контекста.
/// </summary>
public sealed class CurrencyRepository(RatesDbContext db) : ICurrencySyncRepository
{
    private readonly RatesDbContext _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<int> UpsertManyAsync(
        IReadOnlyList<CbrCurrencyEntry> entries,
        DateOnly rateDate,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var count = 0;
        foreach (var entry in entries)
        {
            var normalized = entry.CharCode.Trim().ToUpperInvariant();
            var existing = await _db.Currencies
                .FirstOrDefaultAsync(c => c.Code == normalized, cancellationToken);

            var currency = Currency.Upsert(
                normalized,
                entry.Name,
                entry.NormalizedRate,
                entry.Nominal,
                rateDate,
                now);

            if (existing is null)
            {
                await _db.Currencies.AddAsync(currency, cancellationToken);
            }
            else
            {
                existing.Update(currency.Rate, currency.Nominal, currency.RateDate, currency.UpdatedAt);
            }

            count++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return count;
    }
}

/// <summary>
/// Адаптер read-порта application-слоя поверх EF Core поверх общего контекста.
/// </summary>
public sealed class CurrencyRateReader(RatesDbContext db) : ICurrencyRateReader
{
    private readonly RatesDbContext _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<Currency>> ListByCodesAsync(
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken)
    {
        if (codes.Count == 0)
        {
            return Array.Empty<Currency>();
        }

        var normalized = codes.Select(c => c.Trim().ToUpperInvariant()).ToArray();
        return await _db.Currencies
            .Where(c => normalized.Contains(c.Code))
            .OrderBy(c => c.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Currency>> ListLatestAsync(CancellationToken cancellationToken)
    {
        var list = await _db.Currencies
            .AsNoTracking()
            .Where(c => !_db.Currencies.Any(c2 =>
                c2.Code == c.Code &&
                (c2.RateDate > c.RateDate ||
                 (c2.RateDate == c.RateDate && c2.UpdatedAt > c.UpdatedAt) ||
                 (c2.RateDate == c.RateDate && c2.UpdatedAt == c.UpdatedAt && c2.Id > c.Id))))
            .OrderBy(c => c.Code)
            .ToListAsync(cancellationToken);
        return list;
    }
}

/// <summary>
/// Адаптер read-порта <see cref="Rates.FinanceService.Application.Currencies.IFavoritesLookup"/>.
/// Делает прямой JOIN к таблице избранного в общей БД. Никакого HTTP-вызова
/// в UserService больше не требуется.
/// </summary>
public sealed class FavoritesLookupAdapter(RatesDbContext db) : IFavoritesLookup
{
    private readonly RatesDbContext _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<UserFavoriteWithRate>> ListByUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return [];
        }

        return await _db.UserFavoriteCurrencies
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderBy(f => f.CurrencyCode)
            .GroupJoin(
                _db.Currencies,
                f => f.CurrencyCode,
                c => c.Code,
                (f, currencies) => new { f, currencies })
            .SelectMany(
                x => x.currencies
                    .Where(c => !x.currencies.Any(c2 =>
                        c2.Code == c.Code &&
                        (c2.RateDate > c.RateDate ||
                         (c2.RateDate == c.RateDate && c2.UpdatedAt > c.UpdatedAt) ||
                         (c2.RateDate == c.RateDate && c2.UpdatedAt == c.UpdatedAt && c2.Id > c.Id))))
                    .Take(1)
                    .DefaultIfEmpty(),
                (x, c) => new UserFavoriteWithRate(
                    x.f.CurrencyCode,
                    x.f.AddedAt,
                    c == null
                        ? null
                        : new CurrencyRateSnapshot(
                            c.Code,
                            c.Name,
                            c.Rate,
                            c.Nominal,
                            c.RateDate)))
            .ToListAsync(cancellationToken);
    }
}

using Microsoft.EntityFrameworkCore;
using Rates.BuildingBlocks.Domain;
using Rates.FinanceService.Domain;

namespace Rates.FinanceService.Infrastructure.Persistence;

/// <summary>
/// Репозиторий для агрегатов <see cref="Currency"/>. RatesWorker использует UpsertAsync,
/// чтобы сохранять идемпотентность записей при повторных опросах ЦБ.
/// </summary>
public sealed class CurrencyRepository : IRepository<Currency>, ICurrencyLookup
{
    private readonly FinanceDbContext _db;

    public CurrencyRepository(FinanceDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public Task<Currency?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Currencies.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task AddAsync(Currency entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _db.Currencies.AddAsync(entity, cancellationToken);
    }

    public void Remove(Currency entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _db.Currencies.Remove(entity);
    }

    public Task<Currency?> FindByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Task.FromResult<Currency?>(null);
        }

        var normalized = code.Trim().ToUpperInvariant();
        return _db.Currencies.FirstOrDefaultAsync(c => c.Code == normalized, cancellationToken);
    }

    public Task<List<Currency>> ListByCodesAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken = default)
    {
        if (codes is null || codes.Count == 0)
        {
            return Task.FromResult(new List<Currency>());
        }

        var normalized = codes.Select(c => c.Trim().ToUpperInvariant()).ToArray();
        return _db.Currencies
            .Where(c => normalized.Contains(c.Code))
            .OrderBy(c => c.Code)
            .ToListAsync(cancellationToken);
    }
}

public interface ICurrencyLookup
{
    Task<Currency?> FindByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<List<Currency>> ListByCodesAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken = default);
}
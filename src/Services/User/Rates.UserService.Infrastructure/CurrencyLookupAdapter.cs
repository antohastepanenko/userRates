using Microsoft.EntityFrameworkCore;
using Rates.BuildingBlocks.Persistence;

namespace Rates.UserService.Infrastructure;

/// <summary>
/// Адаптер, валидирующий существование кода валюты в общей таблице <c>currency</c>
/// через общий <see cref="RatesDbContext"/>. Это позволяет UserService валидировать
/// код перед добавлением в избранное, не делая HTTP-вызов в FinanceService.
/// </summary>
public sealed class CurrencyLookupAdapter(RatesDbContext db) : Application.ICurrencyLookup
{
    private readonly RatesDbContext _db = db ?? throw new ArgumentNullException(nameof(db));

    public Task<bool> ExistsAsync(string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Task.FromResult(false);
        }

        var normalized = code.Trim().ToUpperInvariant();
        return _db.Currencies.AsNoTracking().AnyAsync(c => c.Code == normalized, cancellationToken);
    }
}

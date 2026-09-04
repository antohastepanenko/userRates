using Microsoft.EntityFrameworkCore;
using Rates.UserService.Application;
using Rates.UserService.Domain;

namespace Rates.UserService.Infrastructure.Persistence;

/// <summary>
/// Реализация персистентности избранного на базе EF Core. Работает с отдельной таблицей
/// <c>user_favorite_currency</c>, а не с навигацией на строке <c>user</c>, что упрощает
/// отслеживание изменений EF Core и избавляет от устаревших токенов конкурентности.
/// </summary>
public sealed class EfFavoritesRepository : IFavoritesRepository
{
    private readonly IdentityDbContext _db;

    public EfFavoritesRepository(IdentityDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<IReadOnlyList<string>> ListCodesAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return Array.Empty<string>();
        }

        return await _db.UserFavoriteCurrencies
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderBy(f => f.CurrencyCode)
            .Select(f => f.CurrencyCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserFavoriteSummary>> ListWithAddedAtAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return Array.Empty<UserFavoriteSummary>();
        }

        return await _db.UserFavoriteCurrencies
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderBy(f => f.CurrencyCode)
            .Select(f => new UserFavoriteSummary(f.CurrencyCode, f.AddedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task AddIfMissingAsync(Guid userId, string code, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var exists = await _db.UserFavoriteCurrencies
            .AnyAsync(f => f.UserId == userId && f.CurrencyCode == code, cancellationToken);

        if (exists)
        {
            return;
        }

        var entry = UserFavoriteCurrency.Create(userId, code, now);
        await _db.UserFavoriteCurrencies.AddAsync(entry, cancellationToken);
    }

    public async Task<bool> RemoveAsync(Guid userId, string code, CancellationToken cancellationToken)
    {
        var entry = await _db.UserFavoriteCurrencies
            .FirstOrDefaultAsync(f => f.UserId == userId && f.CurrencyCode == code, cancellationToken);

        if (entry is null)
        {
            return false;
        }

        _db.UserFavoriteCurrencies.Remove(entry);
        return true;
    }
}
using Microsoft.EntityFrameworkCore;
using Rates.FinanceService.Domain;
using Rates.UserService.Domain;

namespace Rates.BuildingBlocks.Persistence;

/// <summary>
/// Единый контекст общей базы данных платформы Rates. Содержит таблицы четырёх
/// предметных областей: пользователи (<c>user</c>, <c>refresh_token</c>),
/// избранное (<c>user_favorite_currency</c>), каталог валют (<c>currency</c>).
/// Имена таблиц и колонок задаются вручную в <c>EntityConfigurations</c> и явно
/// приведены к snake_case — Postgres-идиоматическому стилю.
/// </summary>
public sealed class RatesDbContext : DbContext
{
    public RatesDbContext(DbContextOptions<RatesDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<UserFavoriteCurrency> UserFavoriteCurrencies => Set<UserFavoriteCurrency>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Currency> Currencies => Set<Currency>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RatesDbContext).Assembly);
    }
}

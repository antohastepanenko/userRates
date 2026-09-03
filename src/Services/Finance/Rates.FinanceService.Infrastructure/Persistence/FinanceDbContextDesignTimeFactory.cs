using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Rates.FinanceService.Infrastructure.Persistence;

/// <summary>
/// Фабрика времени разработки, которую использует <c>dotnet ef</c> для построения
/// <see cref="FinanceDbContext"/> вне запущенного API-хоста.
/// </summary>
public sealed class FinanceDbContextDesignTimeFactory : IDesignTimeDbContextFactory<FinanceDbContext>
{
    public FinanceDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("EF_FINANCE_CONNECTION")
            ?? "Host=localhost;Database=rates_finance;Username=rates;Password=rates";

        var builder = new DbContextOptionsBuilder<FinanceDbContext>();
        builder.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MigrationsHistoryTable("__ef_migrations_history", FinanceDbContext.SchemaName);
        });

        return new FinanceDbContext(builder.Options);
    }
}
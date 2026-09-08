using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Rates.BuildingBlocks.Persistence;

/// <summary>
/// Фабрика времени разработки для <c>dotnet ef</c>. Использует переменную окружения
/// <c>EF_CONNECTION_STRING</c> или разумный дефолт для локальной Postgres-БД.
/// </summary>
public sealed class RatesDbContextDesignTimeFactory : IDesignTimeDbContextFactory<RatesDbContext>
{
    public RatesDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("EF_CONNECTION_STRING")
            ?? "Host=localhost;Database=rates;Username=rates;Password=rates";

        var builder = new DbContextOptionsBuilder<RatesDbContext>();
        builder.UseRatesNpgsql(connectionString);

        return new RatesDbContext(builder.Options);
    }
}

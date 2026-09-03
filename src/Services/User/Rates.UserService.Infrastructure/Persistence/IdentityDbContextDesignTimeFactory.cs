using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Rates.UserService.Infrastructure.Persistence;

/// <summary>
/// Фабрика времени разработки, которую использует <c>dotnet ef</c> для построения
/// <see cref="IdentityDbContext"/> вне запущенного API-хоста. Читает строку подключения
/// из <c>EF_IDENTITY_CONNECTION</c> либо использует разумное локальное значение по умолчанию.
/// </summary>
public sealed class IdentityDbContextDesignTimeFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("EF_IDENTITY_CONNECTION")
            ?? "Host=localhost;Database=rates_identity;Username=rates;Password=rates";

        var builder = new DbContextOptionsBuilder<IdentityDbContext>();
        builder.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MigrationsHistoryTable("__ef_migrations_history", IdentityDbContext.SchemaName);
        });

        return new IdentityDbContext(builder.Options);
    }
}
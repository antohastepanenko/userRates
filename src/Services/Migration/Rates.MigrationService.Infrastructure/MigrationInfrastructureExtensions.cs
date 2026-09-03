using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rates.FinanceService.Infrastructure.Persistence;
using Rates.UserService.Infrastructure.Persistence;

namespace Rates.MigrationService.Infrastructure;

/// <summary>
/// Регистрирует оба EF-контекста, которые нужны миграционному хосту. Таблицы и схемы
/// объявляются per-service-контекстами; хост миграций владеет лишь жизненным циклом
/// <c>Database.MigrateAsync()</c>.
/// </summary>
public static class MigrationInfrastructureExtensions
{
    public const string IdentityConnectionStringName = "IdentityDb";
    public const string FinanceConnectionStringName = "FinanceDb";

    public static IServiceCollection AddRatesMigrationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var identityConnection = configuration.GetConnectionString(IdentityConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{IdentityConnectionStringName}' is required for MigrationService.");

        var financeConnection = configuration.GetConnectionString(FinanceConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{FinanceConnectionStringName}' is required for MigrationService.");

        services.AddDbContext<IdentityDbContext>(options =>
        {
            options.UseNpgsql(identityConnection, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", IdentityDbContext.SchemaName);
            });
        });

        services.AddDbContext<FinanceDbContext>(options =>
        {
            options.UseNpgsql(financeConnection, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", FinanceDbContext.SchemaName);
            });
        });

        // Исполнитель миграций перечисляет все экземпляры DbContext через DI. EF Core
        // регистрирует только конкретные типы, поэтому пробрасываем каждый контекст к его
        // базовому типу.
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<IdentityDbContext>());
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<FinanceDbContext>());

        return services;
    }
}
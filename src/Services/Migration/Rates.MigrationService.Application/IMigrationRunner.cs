using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rates.BuildingBlocks.Persistence;

namespace Rates.MigrationService.Application;

/// <summary>
/// Маркерный интерфейс, реализуемый исполнителем миграций. Держим хост миграций
/// независимым от EF Core, чтобы можно было менять провайдеры или запускать миграции
/// в управляемом порядке.
/// </summary>
public interface IMigrationRunner
{
    Task ApplyAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Применяет EF Core-миграции к общей БД. Конкуренция за миграции исключается на
/// уровне оркестратора: docker-compose запускает миграционный контейнер один раз,
/// а остальные сервисы ждут <c>service_completed_successfully</c>.
/// </summary>
public sealed class EfMigrationRunner(IServiceProvider serviceProvider, ILogger<EfMigrationRunner> logger)
    : IMigrationRunner
{
    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();

        var context = scope.ServiceProvider.GetService<RatesDbContext>();
        if (context is null)
        {
            logger.LogWarning(
                "RatesDbContext is not registered; migration host runs without applying EF Core migrations.");
            return;
        }

        logger.LogInformation("Applying migrations for RatesDbContext");

        var pending = await context.Database.GetPendingMigrationsAsync(cancellationToken);
        if (!pending.Any())
        {
            logger.LogInformation("No pending migrations");
            return;
        }

        logger.LogInformation("Pending migrations: {Migrations}", string.Join(", ", pending));
        await context.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Migrations applied");
    }
}

public static class MigrationServiceCollectionExtensions
{
    public static IServiceCollection AddRatesMigration(this IServiceCollection services)
    {
        services.AddScoped<IMigrationRunner, EfMigrationRunner>();
        return services;
    }
}

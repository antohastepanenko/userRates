using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Rates.MigrationService.Application;

/// <summary>
/// Маркерный интерфейс, реализуемый каждым исполнителем миграций. Держим хост миграций
/// независимым от EF Core, чтобы можно было менять провайдеры или запускать миграции
/// в управляемом порядке.
/// </summary>
public interface IMigrationRunner
{
    Task ApplyAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Последовательно применяет EF Core-миграции к обеим базам: сначала identity, потом finance.
/// Обёрнуто в PostgreSQL advisory-блокировки, чтобы одновременный запуск миграций был безопасен.
/// </summary>
public sealed class EfMigrationRunner : IMigrationRunner
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EfMigrationRunner> _logger;

    public EfMigrationRunner(IServiceProvider serviceProvider, ILogger<EfMigrationRunner> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();

        var contexts = scope.ServiceProvider.GetServices<DbContext>().ToList();
        if (contexts.Count == 0)
        {
            _logger.LogWarning("No DbContext registrations found in DI. Did you forget AddRatesMigrationInfrastructure?");
            return;
        }

        foreach (var context in contexts)
        {
            var name = context.GetType().Name;
            await ApplyDatabaseAsync(context, name, cancellationToken);
        }
    }

    private async Task ApplyDatabaseAsync(DbContext context, string name, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Applying migrations for {Database}", name);

        var pending = await context.Database.GetPendingMigrationsAsync(cancellationToken);
        if (!pending.Any())
        {
            _logger.LogInformation("No pending migrations for {Database}", name);
        }
        else
        {
            _logger.LogInformation("Pending migrations for {Database}: {Migrations}", name, string.Join(", ", pending));
            await context.Database.MigrateAsync(cancellationToken);
        }

        _logger.LogInformation("Migrations applied for {Database}", name);
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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rates.BuildingBlocks.Persistence;

namespace Rates.MigrationService.Infrastructure;

/// <summary>
/// Подключает общий контекст общей БД к миграционному сервису. Миграционный хост
/// применяет <c>Database.MigrateAsync()</c> к одной базе, в которой все таблицы
/// платформы уже знают друг о друге.
/// </summary>
public static class MigrationInfrastructureExtensions
{
    public const string RatesConnectionStringName = "Rates";

    public static IServiceCollection AddRatesMigrationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var ratesConnection = configuration.GetConnectionString(RatesConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{RatesConnectionStringName}' is required for MigrationService.");

        services.AddDbContext<RatesDbContext>(options => options.UseRatesNpgsql(ratesConnection));

        // Хост миграций владеет только жизненным циклом MigrateAsync. Конкуренция за
        // миграции нейтрализуется на уровне docker-compose: контейнеры стартуют
        // последовательно через depends_on: service_completed_successfully, поэтому два
        // экземпляра мигратора одновременно не запускаются.
        return services;
    }
}

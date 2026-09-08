using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using Rates.BuildingBlocks.Application;
using Rates.BuildingBlocks.Persistence;
using Rates.FinanceService.Application.Cbr;
using Rates.FinanceService.Application.Currencies;
using Rates.FinanceService.Infrastructure.Cbr;
using Rates.FinanceService.Infrastructure.Persistence;

namespace Rates.FinanceService.Infrastructure;

/// <summary>
/// Подключает инфраструктуру FinanceService: общий <see cref="RatesDbContext"/>,
/// репозиторий каталога валют (write), read-порт реестра валют и CBR-клиент.
/// </summary>
public static class FinanceServiceInfrastructureExtensions
{
    private const string RatesConnectionStringName = "Rates";

    public static IServiceCollection AddRatesFinanceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(RatesConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{RatesConnectionStringName}' is required for FinanceService.");

        services.AddDbContext<RatesDbContext>(options => options.UseRatesNpgsql(connectionString));

        services.AddScoped<IUnitOfWorkFactory>(sp => new EfUnitOfWorkFactory(sp.GetRequiredService<RatesDbContext>()));
        services.AddScoped<ICurrencySyncRepository, CurrencyRepository>();
        services.AddScoped<ICurrencyRateReader, CurrencyRateReader>();
        services.AddScoped<IFavoritesLookup, FavoritesLookupAdapter>();

        services.AddSingleton<ICbrRatesParser, CbrRatesParser>();
        services.AddHttpClient<ICbrRatesClient, CbrRatesClient>()
            .ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddPolicyHandler(HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
                .WaitAndRetryAsync(
                    sleepDurations:
                    [
                        TimeSpan.FromSeconds(1),
                        TimeSpan.FromSeconds(5),
                        TimeSpan.FromSeconds(15)
                    ]));

        return services;
    }

    private sealed class EfUnitOfWorkFactory(RatesDbContext db) : IUnitOfWorkFactory
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
            db.SaveChangesAsync(cancellationToken);
    }
}

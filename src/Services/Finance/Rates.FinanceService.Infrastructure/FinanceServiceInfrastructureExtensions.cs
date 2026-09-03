using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using Rates.BuildingBlocks.Domain;
using Rates.FinanceService.Application.Cbr;
using Rates.FinanceService.Application.Currencies;
using Rates.FinanceService.Application.Users;
using Rates.FinanceService.Domain;
using Rates.FinanceService.Infrastructure.Cbr;
using Rates.FinanceService.Infrastructure.Persistence;
using Rates.FinanceService.Infrastructure.Users;

namespace Rates.FinanceService.Infrastructure;

/// <summary>
/// Подключает инфраструктуру FinanceService: EF-контекст и репозиторий валют.
/// </summary>
public static class FinanceServiceInfrastructureExtensions
{
    public const string FinanceConnectionStringName = "FinanceDb";

    public static IServiceCollection AddRatesFinanceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(FinanceConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{FinanceConnectionStringName}' is required for FinanceService.");

        services.AddDbContext<FinanceDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", FinanceDbContext.SchemaName);
            });
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<FinanceDbContext>());
        services.AddScoped<IRepository<Currency>, CurrencyRepository>();
        services.AddScoped<CurrencyRepository>();
        services.AddScoped<ICurrencyLookup>(sp => sp.GetRequiredService<CurrencyRepository>());
        services.AddScoped<ICurrencySyncRepository, CurrencySyncRepository>();
        services.AddScoped<ICurrencyRateReader, CurrencyRateReader>();

        services.AddHttpClient<IUserFavoritesClient, UserFavoritesClient>()
            .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(5))
            .AddPolicyHandler(HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(
                    sleepDurations: new[]
                    {
                        TimeSpan.FromMilliseconds(200),
                        TimeSpan.FromSeconds(1),
                        TimeSpan.FromSeconds(3),
                    }))
            .AddPolicyHandler(HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 2,
                    durationOfBreak: TimeSpan.FromSeconds(30)));

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
                    sleepDurations: new[]
                    {
                        TimeSpan.FromSeconds(1),
                        TimeSpan.FromSeconds(5),
                        TimeSpan.FromSeconds(15),
                    }));

        return services;
    }
}
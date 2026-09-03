using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rates.BuildingBlocks.Application;
using Rates.BuildingBlocks.Infrastructure.Options;
using Rates.BuildingBlocks.Infrastructure.Security;

namespace Rates.BuildingBlocks.Infrastructure;

/// <summary>
/// Универсальный помощник, который регистрирует общие инфраструктурные компоненты,
/// необходимые любому сервису: привязку опций, JWT-сервис, хэшер паролей и HTTP-адаптер
/// текущего пользователя.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddRatesInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<CbrOptions>(configuration.GetSection(CbrOptions.SectionName));
        services.Configure<ServiceEndpointsOptions>(configuration.GetSection(ServiceEndpointsOptions.SectionName));
        services.Configure<InternalServiceOptions>(configuration.GetSection(InternalServiceOptions.SectionName));

        services.AddHttpContextAccessor();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IInternalServiceTokenValidator, InternalServiceTokenValidator>();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        return services;
    }

    public static IConfigurationBuilder AddRatesConfiguration(this IConfigurationBuilder builder)
    {
        // WebApplicationBuilder / HostApplicationBuilder уже загружают appsettings.json,
        // appsettings.{Environment}.json и переменные окружения. Повторная регистрация JSON-
        // файлов привела бы к тому, что они перекрыли бы параметры командной строки,
        // например --urls. Повторно регистрируем переменные окружения последними,
        // чтобы секреты контейнеров имели приоритет.
        builder.AddEnvironmentVariables();
        return builder;
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rates.BuildingBlocks.Application;
using Rates.BuildingBlocks.Infrastructure.Security;
using Rates.BuildingBlocks.Persistence;
using Rates.UserService.Application;
using Rates.UserService.Infrastructure.Auth;
using Rates.UserService.Infrastructure.Persistence;

namespace Rates.UserService.Infrastructure;

/// <summary>
/// Подключает инфраструктуру UserService: общий <see cref="RatesDbContext"/>,
/// репозитории пользователей, избранного и refresh-токенов, адаптер
/// <see cref="ICurrencyLookup"/>, auth-сервисы и BCrypt-хэшер.
/// </summary>
public static class UserServiceInfrastructureExtensions
{
    private const string RatesConnectionStringName = "Rates";

    public static IServiceCollection AddRatesUserInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(RatesConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{RatesConnectionStringName}' is required for UserService.");

        services.AddDbContext<RatesDbContext>(options => options.UseRatesNpgsql(connectionString));

        services.AddScoped<IUnitOfWorkFactory>(sp => new EfUnitOfWorkFactory(sp.GetRequiredService<RatesDbContext>()));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IFavoritesRepository, EfFavoritesRepository>();
        services.AddScoped<IRefreshTokenRepository, EfRefreshTokenRepository>();

        services.AddScoped<ICurrencyLookup, CurrencyLookupAdapter>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<Application.IPasswordHasher, Rates.UserService.Application.Auth.PasswordHasherAdapter>();
        services.AddSingleton<IAccessTokenService, JwtAccessTokenService>();
        services.AddSingleton<Rates.UserService.Application.Auth.IRefreshTokenService,
            Rates.UserService.Application.Auth.RefreshTokenService>();
        services.AddSingleton<Rates.BuildingBlocks.Infrastructure.Security.IPasswordHasher,
            BCryptPasswordHasher>();

        return services;
    }

    private sealed class EfUnitOfWorkFactory(RatesDbContext db) : IUnitOfWorkFactory
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
            db.SaveChangesAsync(cancellationToken);
    }
}

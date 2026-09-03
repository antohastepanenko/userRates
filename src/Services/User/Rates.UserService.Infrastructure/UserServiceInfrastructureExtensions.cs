using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rates.BuildingBlocks.Domain;
using Rates.UserService.Application;
using Rates.UserService.Infrastructure.Auth;
using Rates.UserService.Infrastructure.Persistence;

namespace Rates.UserService.Infrastructure;

/// <summary>
/// Подключает инфраструктуру UserService: EF-контекст, репозитории, auth-сервисы
/// (JWT, refresh-токены, хэширование паролей) и небольшую абстракцию часов.
/// </summary>
public static class UserServiceInfrastructureExtensions
{
    public const string IdentityConnectionStringName = "IdentityDb";

    public static IServiceCollection AddRatesUserInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(IdentityConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{IdentityConnectionStringName}' is required for UserService.");

        services.AddDbContext<IdentityDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", IdentityDbContext.SchemaName);
            });
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<IdentityDbContext>());
        services.AddScoped<IUnitOfWorkFactory>(sp => new EfUnitOfWorkFactory(sp.GetRequiredService<IdentityDbContext>()));

        // Репозитории для application-слоя.
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<UserRepository>();
        services.AddScoped<IRefreshTokenRepository, EfRefreshTokenRepository>();
        services.AddScoped<IFavoritesRepository, EfFavoritesRepository>();

        // Инфраструктурные одиночки.
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<Rates.UserService.Application.IPasswordHasher, PasswordHasherAdapter>();
        services.AddSingleton<IAccessTokenService, JwtAccessTokenService>();
        services.AddSingleton<IRefreshTokenService, RefreshTokenService>();
        services.AddSingleton<Rates.BuildingBlocks.Infrastructure.Security.IPasswordHasher,
            Rates.BuildingBlocks.Infrastructure.Security.BCryptPasswordHasher>();

        return services;
    }

    /// <summary>Адаптер от <see cref="IUnitOfWorkFactory"/> к EF Core DbContext.</summary>
    private sealed class EfUnitOfWorkFactory : IUnitOfWorkFactory
    {
        private readonly IdentityDbContext _db;

        public EfUnitOfWorkFactory(IdentityDbContext db)
        {
            _db = db;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
            _db.SaveChangesAsync(cancellationToken);
    }
}
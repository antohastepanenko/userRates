using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Rates.BuildingBlocks.Infrastructure.Options;
using System.Text;

namespace Rates.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// Регистрирует JWT-bearer-аутентификацию по секции конфигурации <c>Jwt</c>. И шлюз,
/// и нижестоящие сервисы используют одинаковые параметры валидации, поэтому один и тот же
/// ключ подписи действует везде.
/// </summary>
public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddRatesJwt(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        var signingKey = jwtSection["SigningKey"]
            ?? throw new InvalidOperationException(
                "Jwt:SigningKey is required. Set it via user-secrets, environment variable, or secret manager.");

        EnsureValidSigningKey(signingKey, isDevelopment: IsDevelopment());

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSection["Issuer"],
                    ValidateAudience = true,
                    ValidAudience = jwtSection["Audience"],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(signingKey))
                    {
                        KeyId = null,
                    },
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.UniqueName,
                };
            });

        services.AddAuthorization();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        return services;
    }

    /// <summary>
    /// Жёсткая проверка ключа подписи: пустое значение, плейсхолдер и длина меньше 32 байт
    /// считаются ошибкой и приводят к немедленному останову старта сервиса. В Production
    /// дефолтное dev-значение тоже запрещено.
    /// </summary>
    public static void EnsureValidSigningKey(string key, bool isDevelopment)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey is not configured. Set it via env var JWT_SIGNING_KEY or user-secrets.");
        }

        if (key.StartsWith("REPLACE_ME", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey looks like a placeholder ('REPLACE_ME...'). Set it via env var JWT_SIGNING_KEY.");
        }

        if (key.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters long.");
        }

        // В продакшене нельзя стартовать с дефолтным dev-ключом, который лежит в публичном репозитории.
        if (!isDevelopment && key.StartsWith("dev-only-", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey is the bundled dev-only default. Override it for production.");
        }
    }

    private static bool IsDevelopment()
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        return string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase);
    }
}

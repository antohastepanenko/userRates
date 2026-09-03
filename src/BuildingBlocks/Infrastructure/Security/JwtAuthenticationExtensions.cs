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

        if (signingKey.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters long.");
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
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
                    ClockSkew = System.TimeSpan.FromSeconds(30),
                    NameClaimType = System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.UniqueName,
                };
            });

        services.AddAuthorization();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        return services;
    }
}
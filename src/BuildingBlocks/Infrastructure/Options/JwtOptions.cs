namespace Rates.BuildingBlocks.Infrastructure.Options;

/// <summary>
/// Опции, привязанные к секции конфигурации <c>Jwt</c>. <see cref="SigningKey"/> должен
/// поступать из провайдера секретов (переменная окружения / менеджер секретов) —
/// никогда не коммитьте его в систему контроля версий.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "rates";

    public string Audience { get; set; } = "rates.api";

    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenLifetimeMinutes { get; set; } = 15;

    public int RefreshTokenLifetimeDays { get; set; } = 30;
}
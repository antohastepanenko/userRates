namespace Rates.ApiGateway.Api;

/// <summary>
/// URL'ы, используемые readiness-пробой шлюза. Liveness должна оставаться независимой
/// от доступности нижестоящих сервисов; readiness считается работоспособным только когда
/// оба сервиса отвечают.
/// </summary>
public sealed class GatewayHealthOptions
{
    public const string SectionName = "Health";

    public string UserServiceUrl { get; set; } = "http://user-api:8080/health/ready";

    public string FinanceServiceUrl { get; set; } = "http://finance-api:8080/health/ready";

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(2);
}

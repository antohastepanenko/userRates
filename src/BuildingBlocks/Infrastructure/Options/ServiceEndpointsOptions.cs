namespace Rates.BuildingBlocks.Infrastructure.Options;

/// <summary>
/// Опции, привязанные к секции конфигурации <c>ServiceEndpoints</c>, чтобы сервис мог
/// узнать адрес другого сервиса для межсервисных вызовов (без магии с DNS).
/// </summary>
public sealed class ServiceEndpointsOptions
{
    public const string SectionName = "ServiceEndpoints";

    public string UserServiceBaseUrl { get; set; } = "http://user-api:8080";

    public string FinanceServiceBaseUrl { get; set; } = "http://finance-api:8080";

    public string RatesWorkerBaseUrl { get; set; } = "http://rates-worker:8080";
}
using System.Collections.Concurrent;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Rates.ApiGateway.Api;

/// <summary>
/// Проверяет доступность каждого обязательного нижестоящего сервиса. В ответе health-проверки
/// не раскрываются детали исключений, но имена сервисов включаются в структурированные данные,
/// чтобы операторы могли понять, какой зависимости это касается.
/// </summary>
public sealed class DownstreamServicesHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GatewayHealthOptions _options;
    private readonly ILogger<DownstreamServicesHealthCheck> _logger;

    public DownstreamServicesHealthCheck(
        IHttpClientFactory httpClientFactory,
        IOptions<GatewayHealthOptions> options,
        ILogger<DownstreamServicesHealthCheck> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var checks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["user-api"] = _options.UserServiceUrl,
            ["finance-api"] = _options.FinanceServiceUrl,
        };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);

        var statuses = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var tasks = checks.Select(pair => CheckServiceAsync(pair.Key, pair.Value, statuses, timeout.Token));
        await Task.WhenAll(tasks);

        var failed = statuses
            .Where(pair => !string.Equals(pair.Value as string, "healthy", StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        if (failed.Length == 0)
        {
            return HealthCheckResult.Healthy("All mandatory downstream services are ready.", statuses);
        }

        _logger.LogWarning(
            "Gateway readiness failed for downstream services: {Services}",
            string.Join(", ", failed));

        return HealthCheckResult.Unhealthy(
            "One or more mandatory downstream services are unavailable.",
            data: statuses);
    }

    private async Task CheckServiceAsync(
        string serviceName,
        string url,
        ConcurrentDictionary<string, object> statuses,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            statuses[serviceName] = "invalid_url";
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient(nameof(DownstreamServicesHealthCheck));
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            statuses[serviceName] = response.IsSuccessStatusCode
                ? "healthy"
                : $"http_{(int)response.StatusCode}";
        }
        catch (OperationCanceledException)
        {
            statuses[serviceName] = "timeout";
        }
        catch (HttpRequestException)
        {
            statuses[serviceName] = "unreachable";
        }
        catch (Exception ex)
        {
            statuses[serviceName] = "error";
            _logger.LogDebug(ex, "Unexpected error while checking {ServiceName}", serviceName);
        }
    }
}

using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Rates.ApiGateway.Api;
using Rates.BuildingBlocks.Infrastructure;
using Rates.BuildingBlocks.Infrastructure.Security;
using Rates.BuildingBlocks.Infrastructure.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddRatesConfiguration();

builder.Services.Configure<GatewayHealthOptions>(
    builder.Configuration.GetSection(GatewayHealthOptions.SectionName));

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Валидируем каждый access-токен на внешнем крае. UserService и FinanceService
// валидируют тот же токен повторно, поэтому прямое подключение к сервису не может
// обойти авторизацию.
builder.Services.AddRatesJwt(builder.Configuration);
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("authenticated", policy => policy.RequireAuthenticatedUser());
});

builder.Services.AddCors(options =>
{
    static void ConfigureCors(CorsPolicyBuilder policy, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .WithExposedHeaders(CorrelationIdMiddleware.HeaderName);
    }

    options.AddPolicy("RatesCors", policy => ConfigureCors(policy, builder.Configuration));
    options.AddDefaultPolicy(policy => ConfigureCors(policy, builder.Configuration));
});

builder.Services.AddRateLimiter(options =>
{
    var authPermitLimit = Math.Max(1, builder.Configuration.GetValue("RateLimiting:AuthPermitLimit", 10));
    var authWindowSeconds = Math.Max(1, builder.Configuration.GetValue("RateLimiting:AuthWindowSeconds", 60));
    var apiPermitLimit = Math.Max(1, builder.Configuration.GetValue("RateLimiting:ApiPermitLimit", 120));
    var apiWindowSeconds = Math.Max(1, builder.Configuration.GetValue("RateLimiting:ApiWindowSeconds", 60));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;
        response.ContentType = "application/problem+json";
        response.Headers.RetryAfter = GetRetryAfterSeconds(
            context.Lease,
            fallbackSeconds: Math.Max(authWindowSeconds, apiWindowSeconds)).ToString();

        var problem = new
        {
            type = "https://rates.local/errors/rate_limit_exceeded",
            title = "rate_limit_exceeded",
            status = StatusCodes.Status429TooManyRequests,
            detail = "Too many requests. Retry after the indicated interval.",
            traceId = context.HttpContext.TraceIdentifier,
            correlationId = CorrelationIdMiddleware.GetCorrelationId(context.HttpContext),
        };

        await response.WriteAsync(JsonSerializer.Serialize(problem), cancellationToken);
    };

    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetClientPartitionKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authPermitLimit,
                Window = TimeSpan.FromSeconds(authWindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));

    options.AddPolicy("api", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetAuthenticatedPartitionKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = apiPermitLimit,
                Window = TimeSpan.FromSeconds(apiWindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
});

builder.Services.AddHttpClient(nameof(DownstreamServicesHealthCheck))
    .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(3));
builder.Services.AddHealthChecks()
    .AddCheck<DownstreamServicesHealthCheck>(
        "downstream-services",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]);

builder.Services.AddRatesInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Rates API Gateway",
        Version = "v1",
        Description = "Public Rates API routed through the API Gateway.",
    });
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter a valid JWT access token.",
    });
    options.DocumentFilter<Rates.ApiGateway.Api.GatewayOpenApiDocumentFilter>();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRatesCommonPipeline();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "Rates.ApiGateway" }))
    .DisableRateLimiting();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready", StringComparer.OrdinalIgnoreCase),
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
    },
    ResponseWriter = WriteHealthResponseAsync,
}).DisableRateLimiting();

// В YARP сконфигурированы только публичные префиксы API. Внутренние служебные эндпоинты
// никогда не проксируются этим приложением и потому получают обычный ответ 404.
app.MapReverseProxy();

app.Run();

static string GetClientPartitionKey(HttpContext context) =>
    context.Connection.RemoteIpAddress?.ToString() ?? "unknown-client";

static string GetAuthenticatedPartitionKey(HttpContext context) =>
    context.User.FindFirstValue(ClaimTypes.NameIdentifier)
    ?? context.User.FindFirstValue("sub")
    ?? GetClientPartitionKey(context);

static int GetRetryAfterSeconds(RateLimitLease lease, int fallbackSeconds)
{
    if (lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
    {
        return Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
    }

    return Math.Max(1, fallbackSeconds);
}

static async Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json; charset=utf-8";
    var payload = new
    {
        status = report.Status.ToString().ToLowerInvariant(),
        service = "Rates.ApiGateway",
        checks = report.Entries.ToDictionary(
            entry => entry.Key,
            entry => new
            {
                status = entry.Value.Status.ToString().ToLowerInvariant(),
                durationMs = entry.Value.Duration.TotalMilliseconds,
                data = entry.Value.Data.ToDictionary(
                    item => item.Key,
                    item => item.Value?.ToString() ?? string.Empty),
            }),
    };

    await context.Response.WriteAsJsonAsync(payload);
}

public partial class Program;

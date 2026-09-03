using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Rates.BuildingBlocks.Infrastructure.Web;

namespace Rates.Gateway.IntegrationTests;

public sealed class SmokeTests
{
    [Fact]
    public async Task Health_returns_liveness_without_calling_downstream()
    {
        using var factory = await GatewayApplicationFactory.CreateAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("healthy");
        factory.Backend.Requests.Should().Be(0);
    }

    [Fact]
    public async Task Readiness_is_healthy_when_both_downstreams_are_available()
    {
        using var factory = await GatewayApplicationFactory.CreateAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("healthy");
        body.GetProperty("checks").GetProperty("downstream-services").GetProperty("status")
            .GetString().Should().Be("healthy");
        factory.Backend.HealthRequests.Should().Be(2);
    }

    [Fact]
    public async Task Readiness_returns_503_when_downstream_is_unavailable()
    {
        using var factory = await GatewayApplicationFactory.CreateAsync();
        factory.Backend.HealthStatusCode = HttpStatusCode.ServiceUnavailable;
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("unhealthy");
    }

    [Fact]
    public async Task Auth_route_preserves_full_public_path()
    {
        using var factory = await GatewayApplicationFactory.CreateAsync();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { name = "test-user", password = "password" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.Backend.LastRequestPath.Should().Be("/api/v1/auth/login");
    }

    [Fact]
    public async Task Protected_route_without_token_returns_401_and_does_not_reach_backend()
    {
        using var factory = await GatewayApplicationFactory.CreateAsync();
        using var client = factory.CreateClient();
        var before = factory.Backend.Requests;

        var response = await client.GetAsync("/api/v1/finance/currencies/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        factory.Backend.Requests.Should().Be(before);
    }

    [Fact]
    public async Task Protected_route_with_valid_token_is_forwarded()
    {
        using var factory = await GatewayApplicationFactory.CreateAsync();
        using var client = factory.CreateClient();
        var token = GatewayJwt.CreateToken(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "test-user",
            GatewayApplicationFactory.SigningKey);

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/finance/currencies/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.Backend.LastRequestPath.Should().Be("/api/v1/finance/currencies/me");
        factory.Backend.LastAuthorization.Should().StartWith("Bearer ");
    }

    [Fact]
    public async Task Correlation_id_is_forwarded_and_returned()
    {
        using var factory = await GatewayApplicationFactory.CreateAsync();
        const string correlationId = "gateway-test-correlation-123";
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, correlationId);

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { name = "test-user", password = "password" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.Backend.LastCorrelationId.Should().Be(correlationId);
        response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single().Should().Be(correlationId);
    }

    [Fact]
    public async Task Invalid_correlation_id_is_replaced_with_a_safe_gateway_id()
    {
        using var factory = await GatewayApplicationFactory.CreateAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, "invalid value with spaces");

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { name = "test-user", password = "password" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var forwarded = factory.Backend.LastCorrelationId;
        forwarded.Should().NotBeNullOrWhiteSpace();
        forwarded.Should().NotContain(" ");
        forwarded.Should().NotContain("\"");
        response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single().Should().Be(forwarded);
    }

    [Fact]
    public async Task Allowed_cors_preflight_returns_cors_headers()
    {
        using var factory = await GatewayApplicationFactory.CreateAsync();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        request.Headers.Add("Origin", GatewayApplicationFactory.AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        using var client = factory.CreateClient();
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").Single()
            .Should().Be(GatewayApplicationFactory.AllowedOrigin);
        response.Headers.GetValues("Access-Control-Allow-Methods").Single()
            .Should().Contain("POST");
    }

    [Fact]
    public async Task Internal_route_is_not_exposed_by_gateway()
    {
        using var factory = await GatewayApplicationFactory.CreateAsync();
        using var client = factory.CreateClient();
        var before = factory.Backend.Requests;

        var response = await client.GetAsync(
            "/internal/v1/users/11111111-1111-1111-1111-111111111111/favorite-codes");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        factory.Backend.Requests.Should().Be(before);
    }

    [Fact]
    public async Task Auth_rate_limit_returns_429_with_retry_after()
    {
        using var factory = await GatewayApplicationFactory.CreateAsync(authPermitLimit: 3);
        using var client = factory.CreateClient();
        var statuses = new List<HttpStatusCode>();
        var responses = new List<HttpResponseMessage>();

        try
        {
            for (var i = 0; i < 4; i++)
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
                {
                    Content = JsonContent.Create(new { name = "test-user", password = "password" }),
                };
                responses.Add(await client.SendAsync(request));
                statuses.Add(responses[^1].StatusCode);
            }

            statuses.Count(code => code == HttpStatusCode.TooManyRequests).Should().Be(1);
            var rejected = responses.Single(response => response.StatusCode == HttpStatusCode.TooManyRequests);
            rejected.Headers.GetValues("Retry-After").Single().Should().Be("60");
            rejected.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
            var body = await rejected.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("status").GetInt32().Should().Be(429);
            factory.Backend.Requests.Should().Be(3);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task Development_swagger_document_is_available()
    {
        using var factory = await GatewayApplicationFactory.CreateAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();
        document.GetProperty("paths").TryGetProperty("/api/v1/auth/login", out _).Should().BeTrue();
        document.GetProperty("paths").TryGetProperty("/api/v1/finance/currencies/me", out _).Should().BeTrue();
        document.GetProperty("paths").TryGetProperty("/internal/v1/users/{userId}/favorite-codes", out _).Should().BeFalse();
        document.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _).Should().BeTrue();
    }
}

public sealed class GatewayApplicationFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "gateway-test-signing-key-at-least-32-characters";
    public const string AllowedOrigin = "http://localhost:3000";

    private readonly int _authPermitLimit;

    private GatewayApplicationFactory(int authPermitLimit, RecordingBackend backend)
    {
        _authPermitLimit = authPermitLimit;
        Backend = backend;
    }

    public RecordingBackend Backend { get; }

    public static async Task<GatewayApplicationFactory> CreateAsync(int authPermitLimit = 20)
    {
        var backend = new RecordingBackend();
        await backend.StartAsync();
        return new GatewayApplicationFactory(authPermitLimit, backend);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Debug));
        builder.UseSetting("Jwt:Issuer", "rates");
        builder.UseSetting("Jwt:Audience", "rates.api");
        builder.UseSetting("Jwt:SigningKey", GatewayApplicationFactory.SigningKey);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            // Используем полностью in-memory-конфигурацию. Сброс стандартных источников важен:
            // Development-файл appsettings указывает на реальные localhost-порты и не должен
            // перезаписывать динамический адрес backend'а этого теста.
            configuration.Sources.Clear();
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "rates",
                ["Jwt:Audience"] = "rates.api",
                ["Jwt:SigningKey"] = SigningKey,
                ["Cors:AllowedOrigins:0"] = AllowedOrigin,
                ["RateLimiting:AuthPermitLimit"] = _authPermitLimit.ToString(),
                ["RateLimiting:AuthWindowSeconds"] = "60",
                ["RateLimiting:ApiPermitLimit"] = "20",
                ["RateLimiting:ApiWindowSeconds"] = "60",
                ["Health:UserServiceUrl"] = Backend.HealthUri.ToString(),
                ["Health:FinanceServiceUrl"] = Backend.HealthUri.ToString(),

                ["ReverseProxy:Routes:user-logout:ClusterId"] = "user-api",
                ["ReverseProxy:Routes:user-logout:Order"] = "-10",
                ["ReverseProxy:Routes:user-logout:Match:Path"] = "/api/v1/auth/logout",
                ["ReverseProxy:Routes:user-logout:AuthorizationPolicy"] = "authenticated",
                ["ReverseProxy:Routes:user-logout:RateLimiterPolicy"] = "auth",
                ["ReverseProxy:Routes:user-logout:CorsPolicy"] = "RatesCors",
                ["ReverseProxy:Routes:user-logout:Transforms:0:RequestHeadersCopy"] = "true",

                ["ReverseProxy:Routes:user-auth:ClusterId"] = "user-api",
                ["ReverseProxy:Routes:user-auth:Match:Path"] = "/api/v1/auth/{**catch-all}",
                ["ReverseProxy:Routes:user-auth:RateLimiterPolicy"] = "auth",
                ["ReverseProxy:Routes:user-auth:CorsPolicy"] = "RatesCors",
                ["ReverseProxy:Routes:user-auth:Transforms:0:RequestHeadersCopy"] = "true",

                ["ReverseProxy:Routes:user-profile:ClusterId"] = "user-api",
                ["ReverseProxy:Routes:user-profile:Match:Path"] = "/api/v1/users/{**catch-all}",
                ["ReverseProxy:Routes:user-profile:AuthorizationPolicy"] = "authenticated",
                ["ReverseProxy:Routes:user-profile:RateLimiterPolicy"] = "api",
                ["ReverseProxy:Routes:user-profile:CorsPolicy"] = "RatesCors",
                ["ReverseProxy:Routes:user-profile:Transforms:0:RequestHeadersCopy"] = "true",

                ["ReverseProxy:Routes:finance:ClusterId"] = "finance-api",
                ["ReverseProxy:Routes:finance:Match:Path"] = "/api/v1/finance/{**catch-all}",
                ["ReverseProxy:Routes:finance:AuthorizationPolicy"] = "authenticated",
                ["ReverseProxy:Routes:finance:RateLimiterPolicy"] = "api",
                ["ReverseProxy:Routes:finance:CorsPolicy"] = "RatesCors",
                ["ReverseProxy:Routes:finance:Transforms:0:RequestHeadersCopy"] = "true",

                ["ReverseProxy:Clusters:user-api:Destinations:primary:Address"] = Backend.BaseUri.ToString(),
                ["ReverseProxy:Clusters:finance-api:Destinations:primary:Address"] = Backend.BaseUri.ToString(),
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Backend.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        base.Dispose(disposing);
    }
}

public sealed class RecordingBackend : IAsyncDisposable
{
    private readonly object _gate = new();
    private WebApplication? _app;
    private int _requests;
    private int _healthRequests;
    private string? _lastRequestPath;
    private string? _lastCorrelationId;
    private string? _lastAuthorization;

    public HttpStatusCode HealthStatusCode { get; set; } = HttpStatusCode.OK;

    public int Requests
    {
        get
        {
            lock (_gate)
            {
                return _requests;
            }
        }
    }

    public int HealthRequests
    {
        get
        {
            lock (_gate)
            {
                return _healthRequests;
            }
        }
    }

    public string? LastRequestPath
    {
        get
        {
            lock (_gate)
            {
                return _lastRequestPath;
            }
        }
    }

    public string? LastCorrelationId
    {
        get
        {
            lock (_gate)
            {
                return _lastCorrelationId;
            }
        }
    }

    public string? LastAuthorization
    {
        get
        {
            lock (_gate)
            {
                return _lastAuthorization;
            }
        }
    }

    public Uri BaseUri { get; private set; } = null!;

    public Uri HealthUri => new(BaseUri, "health/ready");

    public async Task StartAsync()
    {
        var port = GetFreePort();
        BaseUri = new Uri($"http://127.0.0.1:{port}/");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(RecordingBackend).Assembly.GetName().Name,
            EnvironmentName = "Testing",
        });
        builder.WebHost.UseUrls(BaseUri.ToString());
        builder.Logging.ClearProviders();

        var app = builder.Build();
        app.MapGet("/health/ready", () =>
        {
            lock (_gate)
            {
                _healthRequests++;
            }

            return Results.StatusCode((int)HealthStatusCode);
        });
        app.Map("/{**path}", HandleRequestAsync);

        _app = app;
        await app.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
        }
    }

    private async Task HandleRequestAsync(HttpContext context)
    {
        lock (_gate)
        {
            _requests++;
            _lastRequestPath = context.Request.Path.Value;
            _lastCorrelationId = context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();
            _lastAuthorization = context.Request.Headers.Authorization.ToString();
        }

        context.Response.Headers[CorrelationIdMiddleware.HeaderName] = "downstream-value";
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = "backend",
            path = context.Request.Path.Value,
            correlationId = context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString(),
        });
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}

public static class GatewayJwt
{
    public static string CreateToken(Guid userId, string name, string signingKey)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: "rates",
            audience: "rates.api",
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, name),
                new Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.UniqueName, name),
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }
}

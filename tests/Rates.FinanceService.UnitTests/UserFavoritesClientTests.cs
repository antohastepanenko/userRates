using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Rates.BuildingBlocks.Infrastructure.Options;
using Rates.FinanceService.Infrastructure.Users;

namespace Rates.FinanceService.UnitTests;

public sealed class UserFavoritesClientTests
{
    private const string InternalToken = "unit-test-internal-service-token";
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Sends_internal_token_and_returns_codes_with_added_at()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"userId":"11111111-1111-1111-1111-111111111111","items":[{"code":"USD","addedAt":"2026-09-04T10:00:00+00:00"},{"code":"EUR","addedAt":"2026-09-04T10:05:00+00:00"}]}""",
                Encoding.UTF8,
                "application/json"),
        });
        var client = CreateClient(handler);

        var result = await client.GetFavoritesAsync(UserId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(f => f.Code).Should().Equal("USD", "EUR");
        result.Value.Select(f => f.AddedAt).Should().Equal(
            new DateTimeOffset(2026, 9, 4, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 4, 10, 5, 0, TimeSpan.Zero));
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Headers.GetValues("X-Internal-Service-Token")
            .Single().Should().Be(InternalToken);
        handler.LastRequest.RequestUri!.AbsolutePath.Should()
            .Be($"/internal/v1/users/{UserId:D}/favorite-codes");
    }

    [Fact]
    public async Task Maps_not_found_to_user_not_found()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = CreateClient(handler);

        var result = await client.GetFavoritesAsync(UserId, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("user_not_found");
    }

    [Fact]
    public async Task Maps_timeout_to_unavailable_error()
    {
        var handler = new RecordingHandler(_ => throw new TaskCanceledException("timed out"));
        var client = CreateClient(handler);

        using var timeout = new CancellationTokenSource();
        var result = await client.GetFavoritesAsync(UserId, timeout.Token);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("user_service_timeout");
    }

    private static UserFavoritesClient CreateClient(RecordingHandler handler)
    {
        var http = new HttpClient(handler);
        var endpoints = Options.Create(new ServiceEndpointsOptions
        {
            UserServiceBaseUrl = "http://user-api:8080",
        });
        var internalOptions = Options.Create(new InternalServiceOptions
        {
            SharedToken = InternalToken,
        });

        return new UserFavoritesClient(
            http,
            endpoints,
            internalOptions,
            NullLogger<UserFavoritesClient>.Instance);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_handler(request));
        }
    }
}

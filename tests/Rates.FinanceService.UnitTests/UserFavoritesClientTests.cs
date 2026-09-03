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

    [Fact]
    public async Task Sends_internal_token_and_returns_codes()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"userId\":\"11111111-1111-1111-1111-111111111111\",\"codes\":[\"USD\",\"EUR\"]}",
                Encoding.UTF8,
                "application/json"),
        });
        var client = CreateClient(handler);
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var result = await client.GetFavoriteCodesAsync(userId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Equal("USD", "EUR");
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Headers.GetValues("X-Internal-Service-Token")
            .Single().Should().Be(InternalToken);
        handler.LastRequest.RequestUri!.AbsolutePath.Should()
            .Be($"/internal/v1/users/{userId:D}/favorite-codes");
    }

    [Fact]
    public async Task Maps_not_found_to_user_not_found()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = CreateClient(handler);

        var result = await client.GetFavoriteCodesAsync(
            Guid.Parse("11111111-1111-1111-1111-111111111111"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("user_not_found");
    }

    [Fact]
    public async Task Maps_timeout_to_unavailable_error()
    {
        var handler = new RecordingHandler(_ => throw new TaskCanceledException("timed out"));
        var client = CreateClient(handler);

        using var timeout = new CancellationTokenSource();
        var result = await client.GetFavoriteCodesAsync(
            Guid.Parse("11111111-1111-1111-1111-111111111111"), timeout.Token);

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

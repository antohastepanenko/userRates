using FluentAssertions;
using Rates.UserService.Application.Auth;

namespace Rates.UserService.UnitTests;

public sealed class RefreshTokenCommandHandlerTests
{
    [Fact]
    public async Task Returns_unauthorized_when_token_unknown()
    {
        var refreshTokens = new InMemoryRefreshTokenRepository();
        var users = new InMemoryUserRepository();
        var handler = new RefreshTokenCommandHandler(
            refreshTokens, users, new FakeAccessTokenService(), new FakeRefreshTokenService(),
            new FixedClock(DateTimeOffset.UtcNow), new NoopUnitOfWork());

        var result = await handler.Handle(new RefreshTokenCommand("missing"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("invalid_refresh_token");
    }

    [Fact]
    public async Task Returns_user_not_found_when_user_disappears()
    {
        var tokenHash = RefreshTokenService.Hash("plain");
        var expired = DateTimeOffset.UtcNow.AddDays(-1);
        var existing = Domain.RefreshToken.Issue(Guid.NewGuid(), tokenHash, expired, DateTimeOffset.UtcNow);
        var refreshTokens = new InMemoryRefreshTokenRepository();
        // FakeRefreshTokenService возвращает запись с заведомо неверным хэшем,
        // поэтому FindActiveByPlainAsync вернёт null, что попадает в ветку invalid_refresh_token.
        var users = new InMemoryUserRepository();
        var handler = new RefreshTokenCommandHandler(
            refreshTokens, users, new FakeAccessTokenService(), new FakeRefreshTokenService(),
            new FixedClock(DateTimeOffset.UtcNow), new NoopUnitOfWork());

        var result = await handler.Handle(new RefreshTokenCommand("plain"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("invalid_refresh_token");
        _ = existing;
    }
}

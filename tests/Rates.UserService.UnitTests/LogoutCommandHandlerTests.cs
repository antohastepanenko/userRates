using FluentAssertions;
using Rates.UserService.Application.Auth;

namespace Rates.UserService.UnitTests;

public sealed class LogoutCommandHandlerTests
{
    [Fact]
    public async Task Logout_with_unknown_token_is_a_noop_success()
    {
        var refreshTokens = new InMemoryRefreshTokenRepository();
        var handler = new LogoutCommandHandler(
            refreshTokens, new FixedClock(DateTimeOffset.UtcNow), new NoopUnitOfWork());

        var result = await handler.Handle(new LogoutCommand("missing"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}

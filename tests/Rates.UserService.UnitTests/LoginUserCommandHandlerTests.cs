using FluentAssertions;
using Rates.BuildingBlocks.Domain;
using Rates.UserService.Application.Auth;

namespace Rates.UserService.UnitTests;

public sealed class LoginUserCommandHandlerTests
{
    [Fact]
    public async Task Returns_unauthorized_for_unknown_user()
    {
        var users = new InMemoryUserRepository();
        var handler = new LoginUserCommandHandler(
            users,
            new InMemoryRefreshTokenRepository(),
            new FakePasswordHasher(),
            new FakeAccessTokenService(),
            new FakeRefreshTokenService(),
            new NoopUnitOfWork());

        var result = await handler.Handle(new LoginUserCommand("missing", "secret123"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("invalid_credentials");
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Returns_unauthorized_for_wrong_password()
    {
        var existing = Domain.User.Create("alice", "hash:secret123", DateTimeOffset.UtcNow);
        var users = new InMemoryUserRepository(existing);
        var handler = new LoginUserCommandHandler(
            users,
            new InMemoryRefreshTokenRepository(),
            new FakePasswordHasher(),
            new FakeAccessTokenService(),
            new FakeRefreshTokenService(),
            new NoopUnitOfWork());

        var result = await handler.Handle(new LoginUserCommand("alice", "wrong"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("invalid_credentials");
    }

    [Fact]
    public async Task Returns_tokens_for_valid_credentials()
    {
        var existing = Domain.User.Create("alice", "hash:secret123", DateTimeOffset.UtcNow);
        var users = new InMemoryUserRepository(existing);
        var refreshTokens = new InMemoryRefreshTokenRepository();
        var handler = new LoginUserCommandHandler(
            users,
            refreshTokens,
            new FakePasswordHasher(),
            new FakeAccessTokenService(),
            new FakeRefreshTokenService(),
            new NoopUnitOfWork());

        var result = await handler.Handle(new LoginUserCommand("alice", "secret123"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Tokens.AccessToken.Should().Be("access-token");
        result.Value.Tokens.RefreshToken.Should().Be("refresh-token");
        refreshTokens.SavedToken.Should().NotBeNull();
    }
}

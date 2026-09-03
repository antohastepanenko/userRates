using FluentAssertions;
using Rates.UserService.Application;
using Rates.UserService.Application.Auth;

namespace Rates.UserService.UnitTests;

public sealed class RegisterUserCommandHandlerTests
{
    [Fact]
    public async Task Returns_conflict_when_user_already_exists()
    {
        var existing = DomainFixture.CreateUser("alice");
        var users = new InMemoryUserRepository(existing);
        var refreshTokens = new InMemoryRefreshTokenRepository();
        var passwordHasher = new FakePasswordHasher();
        var accessTokens = new FakeAccessTokenService();
        var refreshService = new FakeRefreshTokenService();
        var clock = new FixedClock(DateTimeOffset.UtcNow);

        var handler = new RegisterUserCommandHandler(
            users, refreshTokens, passwordHasher, accessTokens, refreshService, clock, new NoopUnitOfWork());

        var result = await handler.Handle(new RegisterUserCommand("alice", "secret123"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("user_already_exists");
    }

    [Fact]
    public async Task Creates_user_and_returns_token_pair_on_success()
    {
        var users = new InMemoryUserRepository();
        var refreshTokens = new InMemoryRefreshTokenRepository();
        var passwordHasher = new FakePasswordHasher();
        var accessTokens = new FakeAccessTokenService();
        var refreshService = new FakeRefreshTokenService();
        var clock = new FixedClock(DateTimeOffset.UtcNow);

        var handler = new RegisterUserCommandHandler(
            users, refreshTokens, passwordHasher, accessTokens, refreshService, clock, new NoopUnitOfWork());

        var result = await handler.Handle(new RegisterUserCommand("bob", "secret123"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Tokens.AccessToken.Should().Be("access-token");
        result.Value.Name.Should().Be("bob");
        users.SavedUser.Should().NotBeNull();
        refreshTokens.SavedToken.Should().NotBeNull();
    }
}

internal static class DomainFixture
{
    public static Domain.User CreateUser(string name)
    {
        var user = Domain.User.Create(name, "hash", DateTimeOffset.UtcNow);
        return user;
    }
}

internal sealed class InMemoryUserRepository : IUserRepository
{
    private readonly Domain.User? _existing;
    public Domain.User? SavedUser { get; private set; }

    public InMemoryUserRepository(Domain.User? existing = null)
    {
        _existing = existing;
    }

    public Task<Domain.User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_existing);

    public Task<Domain.User?> FindByNameAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult(_existing);

    public async Task AddAsync(Domain.User user, CancellationToken cancellationToken)
    {
        SavedUser = user;
        await Task.CompletedTask;
    }
}

internal sealed class InMemoryRefreshTokenRepository : IRefreshTokenRepository
{
    public Domain.RefreshToken? SavedToken { get; private set; }

    public async Task AddAsync(Domain.RefreshToken token, CancellationToken cancellationToken)
    {
        SavedToken = token;
        await Task.CompletedTask;
    }

    public Task<Domain.RefreshToken?> FindActiveByPlainAsync(string plain, CancellationToken cancellationToken) =>
        Task.FromResult<Domain.RefreshToken?>(null);

    public Task RevokeAsync(Guid id, DateTimeOffset now, Guid? replacedById, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"hash:{password}";
    public bool Verify(string password, string hash) => hash == $"hash:{password}";
}

internal sealed class FakeAccessTokenService : IAccessTokenService
{
    public (string Token, DateTimeOffset ExpiresAt) Issue(Guid userId, string name) =>
        ("access-token", DateTimeOffset.UtcNow.AddMinutes(15));
}

internal sealed class FakeRefreshTokenService : IRefreshTokenService
{
    public (Domain.RefreshToken Token, string Plain) Issue(Guid userId)
    {
        var token = Domain.RefreshToken.Issue(userId, "hash", DateTimeOffset.UtcNow.AddDays(30), DateTimeOffset.UtcNow);
        return (token, "refresh-token");
    }
}

internal sealed class FixedClock : IClock
{
    public DateTimeOffset UtcNow { get; }
    public FixedClock(DateTimeOffset now) => UtcNow = now;
}

internal sealed class NoopUnitOfWork : IUnitOfWorkFactory
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(0);
}
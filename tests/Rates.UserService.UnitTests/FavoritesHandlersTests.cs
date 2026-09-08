using System.Security.Claims;
using FluentAssertions;
using Rates.BuildingBlocks.Application;
using Rates.BuildingBlocks.Domain;
using Rates.UserService.Application;
using Rates.UserService.Application.Favorites;

namespace Rates.UserService.UnitTests;

public sealed class AddFavoriteCurrencyCommandHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Returns_unauthorized_without_user()
    {
        var handler = new AddFavoriteCurrencyCommandHandler(
            new StubCurrentUser(isAuthenticated: false, userId: null),
            new InMemoryFavoritesRepository(),
            new FakeCurrencyLookup(exists: true),
            new FixedClock(DateTimeOffset.UtcNow),
            new NoopUnitOfWork());

        var result = await handler.Handle(new AddFavoriteCurrencyCommand("USD"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Returns_validation_error_when_currency_unknown()
    {
        var handler = new AddFavoriteCurrencyCommandHandler(
            new StubCurrentUser(isAuthenticated: true, userId: UserId),
            new InMemoryFavoritesRepository(),
            new FakeCurrencyLookup(exists: false),
            new FixedClock(DateTimeOffset.UtcNow),
            new NoopUnitOfWork());

        var result = await handler.Handle(new AddFavoriteCurrencyCommand("ZZZ"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("unknown_currency");
    }

    [Fact]
    public async Task Adds_favorite_when_currency_exists()
    {
        var favorites = new InMemoryFavoritesRepository();
        var handler = new AddFavoriteCurrencyCommandHandler(
            new StubCurrentUser(isAuthenticated: true, userId: UserId),
            favorites,
            new FakeCurrencyLookup(exists: true),
            new FixedClock(DateTimeOffset.UtcNow),
            new NoopUnitOfWork());

        var result = await handler.Handle(new AddFavoriteCurrencyCommand("usd"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        favorites.Added.Should().Contain(("USD", UserId));
    }

    [Fact]
    public async Task Returns_conflict_via_db_update_for_unique_violation()
    {
        var favorites = new InMemoryFavoritesRepository();
        var uow = new ThrowingUniqueViolationUnitOfWork();
        var handler = new AddFavoriteCurrencyCommandHandler(
            new StubCurrentUser(isAuthenticated: true, userId: UserId),
            favorites,
            new FakeCurrencyLookup(exists: true),
            new FixedClock(DateTimeOffset.UtcNow),
            uow);

        var result = await handler.Handle(new AddFavoriteCurrencyCommand("USD"), CancellationToken.None);

        // Идемпотентный успех — добавление уже существующего в БД вхождения не считается ошибкой.
        result.IsSuccess.Should().BeTrue();
    }
}

public sealed class RemoveFavoriteCurrencyCommandHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Removes_existing_favorite()
    {
        var favorites = new InMemoryFavoritesRepository();
        await favorites.AddIfMissingAsync(UserId, "USD", DateTimeOffset.UtcNow, CancellationToken.None);
        var handler = new RemoveFavoriteCurrencyCommandHandler(
            new StubCurrentUser(isAuthenticated: true, userId: UserId),
            favorites,
            new NoopUnitOfWork());

        var result = await handler.Handle(new RemoveFavoriteCurrencyCommand("USD"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task Removing_missing_favorite_returns_false()
    {
        var favorites = new InMemoryFavoritesRepository();
        var handler = new RemoveFavoriteCurrencyCommandHandler(
            new StubCurrentUser(isAuthenticated: true, userId: UserId),
            favorites,
            new NoopUnitOfWork());

        var result = await handler.Handle(new RemoveFavoriteCurrencyCommand("USD"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }
}

public sealed class GetFavoriteCurrenciesQueryHandlerTests
{
    [Fact]
    public async Task Returns_user_favorites_with_added_at()
    {
        var favorites = new InMemoryFavoritesRepository();
        var userId = Guid.NewGuid();
        await favorites.AddIfMissingAsync(userId, "USD", DateTimeOffset.UtcNow, CancellationToken.None);
        await favorites.AddIfMissingAsync(userId, "EUR", DateTimeOffset.UtcNow, CancellationToken.None);

        var handler = new GetFavoriteCurrenciesQueryHandler(
            new StubCurrentUser(isAuthenticated: true, userId: userId),
            favorites);

        var result = await handler.Handle(new GetFavoriteCurrenciesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(v => v.Code).Should().Contain(new[] { "USD", "EUR" });
    }
}

internal sealed class InMemoryFavoritesRepository : IFavoritesRepository
{
    private readonly Dictionary<(Guid, string), DateTimeOffset> _store = new();

    public List<(string Code, Guid UserId)> Added => _store.Keys.Select(k => (k.Item2, k.Item1)).ToList();

    public Task<IReadOnlyList<string>> ListCodesAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(_store.Where(k => k.Key.Item1 == userId)
            .Select(k => k.Key.Item2).OrderBy(c => c).ToArray());

    public Task<IReadOnlyList<UserFavoriteSummary>> ListWithAddedAtAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<UserFavoriteSummary>>(
            _store.Where(k => k.Key.Item1 == userId)
                .OrderBy(k => k.Key.Item2)
                .Select(k => new UserFavoriteSummary(k.Key.Item2, k.Value))
                .ToArray());

    public Task AddIfMissingAsync(Guid userId, string code, DateTimeOffset now, CancellationToken cancellationToken)
    {
        _store[(userId, code)] = now;
        return Task.CompletedTask;
    }

    public Task<bool> RemoveAsync(Guid userId, string code, CancellationToken cancellationToken)
    {
        return Task.FromResult(_store.Remove((userId, code)));
    }
}

internal sealed class FakeCurrencyLookup : ICurrencyLookup
{
    private readonly bool _exists;
    public FakeCurrencyLookup(bool exists) => _exists = exists;
    public Task<bool> ExistsAsync(string code, CancellationToken cancellationToken) =>
        Task.FromResult(_exists);
}

internal sealed class StubCurrentUser : ICurrentUser
{
    public StubCurrentUser(bool isAuthenticated, Guid? userId)
    {
        IsAuthenticated = isAuthenticated;
        UserId = userId;
    }
    public Guid? UserId { get; }
    public string Name => "test-user";
    public bool IsAuthenticated { get; }
    public IReadOnlyCollection<Claim> Claims => Array.Empty<Claim>();
}

using System.Security.Claims;
using FluentAssertions;
using Moq;
using Rates.BuildingBlocks.Application;
using Rates.BuildingBlocks.Domain;
using Rates.FinanceService.Application.Currencies;

namespace Rates.FinanceService.UnitTests;

public sealed class GetUserCurrencyRatesQueryHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset AddedAt =
        new(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Returns_unauthorized_without_an_authenticated_user()
    {
        var favorites = new Mock<IFavoritesLookup>(MockBehavior.Strict);
        var handler = new GetUserCurrencyRatesQueryHandler(
            new StubCurrentUser(isAuthenticated: false, userId: null), favorites.Object);

        var result = await handler.Handle(new GetUserCurrencyRatesQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        favorites.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Returns_only_available_rates_for_the_current_users_favorites()
    {
        var snapshotUsd = new CurrencyRateSnapshot("USD", "US Dollar", 90.12567891m, 1, new DateOnly(2026, 9, 3));
        var snapshotEur = new CurrencyRateSnapshot("EUR", "Euro", 100.2m, 1, new DateOnly(2026, 9, 3));
        var entries = new[]
        {
            new UserFavoriteWithRate("USD", AddedAt, snapshotUsd),
            new UserFavoriteWithRate("EUR", AddedAt.AddMinutes(5), snapshotEur),
            new UserFavoriteWithRate("JPY", AddedAt.AddMinutes(15), null),
        };

        var favorites = new Mock<IFavoritesLookup>();
        favorites
            .Setup(f => f.ListByUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var handler = new GetUserCurrencyRatesQueryHandler(
            new StubCurrentUser(isAuthenticated: true, userId: UserId), favorites.Object);

        var result = await handler.Handle(new GetUserCurrencyRatesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(item => item.Code).Should().BeEquivalentTo(new[] { "EUR", "USD" });
        result.Value.Items.Single(item => item.Code == "USD")
            .Rate.Should().Be(90.1257m);
        result.Value.MissingCodes.Should().Equal("JPY");
        result.Value.AsOf.Should().Be(new DateOnly(2026, 9, 3));
        result.Value.Items.All(item => item.AddedAt is not null).Should().BeTrue();
        result.Value.Items.Single(item => item.Code == "USD").AddedAt.Should().Be(AddedAt);
        favorites.Verify(f => f.ListByUserAsync(UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Returns_empty_response_when_user_has_no_favorites()
    {
        var favorites = new Mock<IFavoritesLookup>();
        favorites
            .Setup(f => f.ListByUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserFavoriteWithRate>());

        var handler = new GetUserCurrencyRatesQueryHandler(
            new StubCurrentUser(isAuthenticated: true, userId: UserId), favorites.Object);

        var result = await handler.Handle(new GetUserCurrencyRatesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AsOf.Should().BeNull();
        result.Value.Items.Should().BeEmpty();
        result.Value.MissingCodes.Should().BeEmpty();
        favorites.Verify(f => f.ListByUserAsync(UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class StubCurrentUser(bool isAuthenticated, Guid? userId) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;
        public string? Name => "test-user";
        public bool IsAuthenticated { get; } = isAuthenticated;
        public IReadOnlyCollection<Claim> Claims => [];
    }
}

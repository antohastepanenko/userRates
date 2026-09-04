using System.Security.Claims;
using FluentAssertions;
using Rates.BuildingBlocks.Application;
using Rates.BuildingBlocks.Domain;
using Rates.FinanceService.Application.Currencies;
using Rates.FinanceService.Application.Users;
using Rates.FinanceService.Domain;

namespace Rates.FinanceService.UnitTests;

public sealed class GetUserCurrencyRatesQueryHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset AddedAt =
        new(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Returns_unauthorized_without_an_authenticated_user()
    {
        var favorites = new StubFavoritesClient(Result<IReadOnlyList<FavoriteEntry>>.Ok(new[] { new FavoriteEntry("USD", AddedAt) }));
        var rates = new StubCurrencyRateReader(Array.Empty<Currency>());
        var handler = new GetUserCurrencyRatesQueryHandler(
            new StubCurrentUser(isAuthenticated: false, userId: null), favorites, rates);

        var result = await handler.Handle(new GetUserCurrencyRatesQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        favorites.Calls.Should().Be(0);
        rates.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Returns_only_available_rates_for_the_current_users_favorites()
    {
        var favorites = new StubFavoritesClient(
            Result<IReadOnlyList<FavoriteEntry>>.Ok(new[]
            {
                new FavoriteEntry("usd", AddedAt),
                new FavoriteEntry("EUR", AddedAt.AddMinutes(5)),
                new FavoriteEntry("USD", AddedAt.AddMinutes(10)),
                new FavoriteEntry("JPY", AddedAt.AddMinutes(15)),
            }));
        var usd = Currency.Upsert(
            "USD", "US Dollar", 90.12567891m, 1, new DateOnly(2026, 9, 3), DateTimeOffset.UtcNow);
        var eur = Currency.Upsert(
            "EUR", "Euro", 100.2m, 1, new DateOnly(2026, 9, 3), DateTimeOffset.UtcNow);
        var rates = new StubCurrencyRateReader(new[] { eur, usd });
        var handler = new GetUserCurrencyRatesQueryHandler(
            new StubCurrentUser(isAuthenticated: true, userId: UserId), favorites, rates);

        var result = await handler.Handle(new GetUserCurrencyRatesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(item => item.Code).Should().Equal("EUR", "USD");
        result.Value.Items.Single(item => item.Code == "USD")
            .Rate.Should().Be(90.1257m);
        result.Value.MissingCodes.Should().Equal("JPY");
        result.Value.AsOf.Should().Be(new DateOnly(2026, 9, 3));
        result.Value.Items.All(item => item.AddedAt is not null).Should().BeTrue();
        result.Value.Items.Single(item => item.Code == "USD").AddedAt.Should().Be(AddedAt);
        favorites.LastUserId.Should().Be(UserId);
        rates.LastCodes.Should().Equal("EUR", "JPY", "USD");
    }

    [Fact]
    public async Task Returns_empty_response_when_user_has_no_favorites()
    {
        var favorites = new StubFavoritesClient(
            Result<IReadOnlyList<FavoriteEntry>>.Ok(Array.Empty<FavoriteEntry>()));
        var rates = new StubCurrencyRateReader(Array.Empty<Currency>());
        var handler = new GetUserCurrencyRatesQueryHandler(
            new StubCurrentUser(isAuthenticated: true, userId: UserId), favorites, rates);

        var result = await handler.Handle(new GetUserCurrencyRatesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AsOf.Should().BeNull();
        result.Value.Items.Should().BeEmpty();
        result.Value.MissingCodes.Should().BeEmpty();
        rates.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Propagates_user_service_failure_without_reading_currency_catalog()
    {
        var failure = Result<IReadOnlyList<FavoriteEntry>>.Failure(
            Error.Unavailable("user_service_unavailable", "UserService is temporarily unavailable."));
        var favorites = new StubFavoritesClient(failure);
        var rates = new StubCurrencyRateReader(Array.Empty<Currency>());
        var handler = new GetUserCurrencyRatesQueryHandler(
            new StubCurrentUser(isAuthenticated: true, userId: UserId), favorites, rates);

        var result = await handler.Handle(new GetUserCurrencyRatesQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("user_service_unavailable");
        rates.Calls.Should().Be(0);
    }

    private sealed class StubCurrentUser : ICurrentUser
    {
        public StubCurrentUser(bool isAuthenticated, Guid? userId)
        {
            IsAuthenticated = isAuthenticated;
            UserId = userId;
        }

        public Guid? UserId { get; }
        public string? Name => "test-user";
        public bool IsAuthenticated { get; }
        public IReadOnlyCollection<Claim> Claims => Array.Empty<Claim>();
    }

    private sealed class StubFavoritesClient : IUserFavoritesClient
    {
        private readonly Result<IReadOnlyList<FavoriteEntry>> _result;

        public StubFavoritesClient(Result<IReadOnlyList<FavoriteEntry>> result)
        {
            _result = result;
        }

        public int Calls { get; private set; }
        public Guid? LastUserId { get; private set; }

        public Task<Result<IReadOnlyList<FavoriteEntry>>> GetFavoritesAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            Calls++;
            LastUserId = userId;
            return Task.FromResult(_result);
        }
    }

    private sealed class StubCurrencyRateReader : ICurrencyRateReader
    {
        private readonly IReadOnlyList<Currency> _currencies;

        public StubCurrencyRateReader(IReadOnlyList<Currency> currencies)
        {
            _currencies = currencies;
        }

        public int Calls { get; private set; }
        public int LatestCalls { get; private set; }
        public IReadOnlyCollection<string> LastCodes { get; private set; } = Array.Empty<string>();
        public IReadOnlyList<Currency> LatestResponse { get; set; } = Array.Empty<Currency>();

        public Task<IReadOnlyList<Currency>> ListByCodesAsync(
            IReadOnlyCollection<string> codes,
            CancellationToken cancellationToken)
        {
            Calls++;
            LastCodes = codes.ToArray();
            return Task.FromResult(_currencies);
        }

        public Task<IReadOnlyList<Currency>> ListLatestAsync(CancellationToken cancellationToken)
        {
            LatestCalls++;
            return Task.FromResult(LatestResponse);
        }
    }
}

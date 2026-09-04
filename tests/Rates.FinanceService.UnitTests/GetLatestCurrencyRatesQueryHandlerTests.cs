using FluentAssertions;
using Rates.FinanceService.Application.Currencies;
using Rates.FinanceService.Domain;

namespace Rates.FinanceService.UnitTests;

public sealed class GetLatestCurrencyRatesQueryHandlerTests
{
    [Fact]
    public async Task Returns_empty_response_when_catalog_is_empty()
    {
        var rates = new StubCurrencyRateReader(Array.Empty<Currency>());
        var handler = new GetLatestCurrencyRatesQueryHandler(rates);

        var result = await handler.Handle(new GetLatestCurrencyRatesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AsOf.Should().BeNull();
        result.Value.Items.Should().BeEmpty();
        rates.LatestCalls.Should().Be(1);
    }

    [Fact]
    public async Task Forwards_repository_rows_sorted_by_code_and_computes_as_of_as_max_rate_date()
    {
        // Репозиторий уже отфильтровал дубликаты по (Code, RateDate DESC, UpdatedAt DESC)
        // на уровне SQL — handler не должен повторно фильтрацию. Здесь проверяем только,
        // что handler пробрасывает данные и считает AsOf = MAX(RateDate).
        var usd = Currency.Upsert(
            "USD", "US Dollar", 90.125m, 1, new DateOnly(2026, 9, 4), DateTimeOffset.UtcNow);
        var eur = Currency.Upsert(
            "EUR", "Euro", 100.2m, 1, new DateOnly(2026, 9, 3), DateTimeOffset.UtcNow);
        var rates = new StubCurrencyRateReader(Array.Empty<Currency>())
        {
            LatestResponse = new[] { eur, usd },
        };
        var handler = new GetLatestCurrencyRatesQueryHandler(rates);

        var result = await handler.Handle(new GetLatestCurrencyRatesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Select(i => i.Code).Should().Equal("EUR", "USD");
        result.Value.Items.Single(i => i.Code == "USD").Rate.Should().Be(90.125m);
        result.Value.AsOf.Should().Be(new DateOnly(2026, 9, 4));
    }

    [Fact]
    public async Task Does_not_depend_on_user_favorites()
    {
        var rates = new StubCurrencyRateReader(Array.Empty<Currency>())
        {
            LatestResponse = new[]
            {
                Currency.Upsert("USD", "US Dollar", 90m, 1, new DateOnly(2026, 9, 4), DateTimeOffset.UtcNow),
            },
        };
        var handler = new GetLatestCurrencyRatesQueryHandler(rates);

        var result = await handler.Handle(new GetLatestCurrencyRatesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
    }

    private sealed class StubCurrencyRateReader : ICurrencyRateReader
    {
        public StubCurrencyRateReader(IReadOnlyList<Currency> _)
        {
        }

        public int LatestCalls { get; private set; }
        public IReadOnlyList<Currency> LatestResponse { get; set; } = Array.Empty<Currency>();

        public Task<IReadOnlyList<Currency>> ListByCodesAsync(
            IReadOnlyCollection<string> codes,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("ListByCodesAsync should not be called for the latest-rates query.");
        }

        public Task<IReadOnlyList<Currency>> ListLatestAsync(CancellationToken cancellationToken)
        {
            LatestCalls++;
            return Task.FromResult(LatestResponse);
        }
    }
}

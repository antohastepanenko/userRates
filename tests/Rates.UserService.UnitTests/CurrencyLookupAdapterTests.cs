using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Rates.BuildingBlocks.Persistence;
using Rates.FinanceService.Domain;
using Rates.UserService.Infrastructure;

namespace Rates.UserService.UnitTests;

public sealed class CurrencyLookupAdapterTests
{
    [Fact]
    public async Task Returns_true_when_currency_exists()
    {
        var db = NewDbContext();
        db.Currencies.Add(Currency.Upsert("USD", "US Dollar", 90m, 1, new DateOnly(2026, 9, 5), DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        var adapter = new CurrencyLookupAdapter(db);

        (await adapter.ExistsAsync("usd", CancellationToken.None)).Should().BeTrue();
        (await adapter.ExistsAsync("USD", CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task Returns_false_when_currency_missing()
    {
        var adapter = new CurrencyLookupAdapter(NewDbContext());

        (await adapter.ExistsAsync("ZZZ", CancellationToken.None)).Should().BeFalse();
    }

    private static RatesDbContext NewDbContext()
    {
        var options = new DbContextOptionsBuilder<RatesDbContext>()
            .UseInMemoryDatabase($"currency-lookup-{Guid.NewGuid()}")
            .Options;
        return new RatesDbContext(options);
    }
}

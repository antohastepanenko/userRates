using FluentAssertions;

namespace Rates.FinanceService.IntegrationTests;

/// <summary>
/// Лёгкая проверка композиции хоста. Сценарии с базой данных будут добавлены в интеграционный
/// набор после включения CI-фикстуры с Postgres.
/// </summary>
public sealed class SmokeTests
{
    [Fact]
    public void Finance_api_assembly_contains_public_entry_point()
    {
        typeof(Program).Assembly.GetName().Name.Should().Be("Rates.FinanceService.Api");
    }
}

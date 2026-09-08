using FluentAssertions;

namespace Rates.UserService.IntegrationTests;

/// <summary>
/// Лёгкая проверка композиции UserService Api. Полные сценарии с WebApplicationFactory
/// и Postgres будут добавлены после включения CI-фикстуры с базой.
/// </summary>
public sealed class SmokeTests
{
    [Fact]
    public void User_api_assembly_contains_public_entry_point()
    {
        typeof(Program).Assembly.GetName().Name.Should().Be("Rates.UserService.Api");
    }
}

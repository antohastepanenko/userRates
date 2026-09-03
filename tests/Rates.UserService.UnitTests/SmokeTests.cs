using FluentAssertions;

namespace Rates.UserService.UnitTests;

/// <summary>
/// Тест-заглушка этапа 2. На этапе 5 будет заменён реальными тестами обработчиков.
/// </summary>
public sealed class SmokeTests
{
    [Fact]
    public void Application_assembly_loads()
    {
        var assembly = typeof(Rates.UserService.Application.AssemblyMarker).Assembly;

        assembly.GetTypes().Should().NotBeEmpty();
    }
}
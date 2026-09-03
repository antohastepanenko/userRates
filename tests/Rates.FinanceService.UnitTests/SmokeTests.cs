using FluentAssertions;

namespace Rates.FinanceService.UnitTests;

public sealed class SmokeTests
{
    [Fact]
    public void Application_assembly_loads()
    {
        var assembly = typeof(Rates.FinanceService.Application.AssemblyMarker).Assembly;

        assembly.GetTypes().Should().NotBeEmpty();
    }
}
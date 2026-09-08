using FluentAssertions;

namespace Rates.UserService.UnitTests;

public sealed class SmokeTests
{
    [Fact]
    public void Application_assembly_loads()
    {
        var assembly = typeof(Application.AssemblyMarker).Assembly;

        assembly.GetTypes().Should().NotBeEmpty();
    }
}

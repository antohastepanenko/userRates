using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Rates.BuildingBlocks.Infrastructure.Options;
using Rates.BuildingBlocks.Infrastructure.Security;

namespace Rates.FinanceService.UnitTests;

public sealed class FinanceServiceInfrastructureRegistrationTests
{
    [Fact]
    public void Internal_service_token_validator_uses_constant_time_contract()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(new InternalServiceOptions
        {
            SharedToken = "shared-token",
        }));
        services.AddSingleton<IInternalServiceTokenValidator, InternalServiceTokenValidator>();

        using var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IInternalServiceTokenValidator>();

        validator.IsValid("shared-token").Should().BeTrue();
        validator.IsValid("wrong-token").Should().BeFalse();
        validator.IsValid(null).Should().BeFalse();
    }
}

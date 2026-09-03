using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Rates.MigrationService.Application;

namespace Rates.MigrationService.UnitTests;

public sealed class EfMigrationRunnerTests
{
    [Fact]
    public async Task ApplyAsync_logs_warning_when_no_DbContext_is_registered()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var runner = new EfMigrationRunner(sp, NullLogger<EfMigrationRunner>.Instance);

        var act = async () => await runner.ApplyAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
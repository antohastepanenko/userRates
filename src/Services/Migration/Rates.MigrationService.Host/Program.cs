using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rates.BuildingBlocks.Infrastructure;
using Rates.MigrationService.Application;
using Rates.MigrationService.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddRatesConfiguration();

builder.Services.AddLogging(b =>
{
    b.AddSimpleConsole(o =>
    {
        o.IncludeScopes = true;
        o.TimestampFormat = "HH:mm:ss ";
    });
});

builder.Services.AddRatesMigrationInfrastructure(builder.Configuration);
builder.Services.AddRatesMigration();

using var host = builder.Build();

var runner = host.Services.GetRequiredService<IMigrationRunner>();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Rates.MigrationService.Host");

try
{
    logger.LogInformation("Applying database migrations...");
    await runner.ApplyAsync(CancellationToken.None);
    logger.LogInformation("Migrations completed successfully.");
    return 0;
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Migration failed.");
    return 1;
}
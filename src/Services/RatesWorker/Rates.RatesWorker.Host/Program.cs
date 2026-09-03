using Rates.BuildingBlocks.Application;
using Rates.BuildingBlocks.Infrastructure;
using Rates.FinanceService.Infrastructure;
using Rates.RatesWorker.Host;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddRatesConfiguration();

builder.Services.AddRatesInfrastructure(builder.Configuration);
builder.Services.AddRatesFinanceInfrastructure(builder.Configuration);
builder.Services.AddRatesApplication(typeof(Rates.FinanceService.Application.AssemblyMarker).Assembly);

builder.Services.AddHostedService<CbrRatesSyncWorker>();

var host = builder.Build();
host.Run();
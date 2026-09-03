using MediatR;
using Microsoft.Extensions.Options;
using Rates.BuildingBlocks.Infrastructure.Options;
using Rates.FinanceService.Application.Cbr;

namespace Rates.RatesWorker.Host;

/// <summary>
/// Долгоживущий фоновый процесс, который с настраиваемым интервалом гоняет пару
/// GetCbrDailyRatesQuery / SyncCurrencyRatesCommand.
/// </summary>
public sealed class CbrRatesSyncWorker(
    IServiceProvider serviceProvider,
    ILogger<CbrRatesSyncWorker> logger,
    IOptions<CbrOptions> options)
    : BackgroundService
{
    private readonly CbrOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "RatesWorker starting. Url={Url} Interval={Interval} RunOnStartup={RunOnStartup}",
            _options.Url,
            _options.UpdateInterval,
            _options.RunOnStartup);

        if (_options.RunOnStartup)
        {
            await RunOnceSafelyAsync(stoppingToken);
        }

        using var timer = new PeriodicTimer(_options.UpdateInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunOnceSafelyAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // мягкое завершение работы
        }

        logger.LogInformation("RatesWorker stopping.");
    }

    private async Task RunOnceSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var fetch = await mediator.Send(new GetCbrDailyRatesQuery(), cancellationToken);
            if (fetch.IsFailure)
            {
                logger.LogWarning(
                    "CBR fetch failed: {Code} - {Message}. Keeping previous rates.",
                    fetch.Error.Code,
                    fetch.Error.Message);
                return;
            }

            var sync = await mediator.Send(new SyncCurrencyRatesCommand(fetch.Value), cancellationToken);
            if (sync.IsFailure)
            {
                logger.LogWarning(
                    "CBR sync failed: {Code} - {Message}",
                    sync.Error.Code,
                    sync.Error.Message);
                return;
            }

            logger.LogInformation(
                "CBR sync succeeded. RateDate={RateDate} UpsertedCount={Count}",
                sync.Value.RateDate,
                sync.Value.UpsertedCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during CBR sync run.");
        }
    }
}

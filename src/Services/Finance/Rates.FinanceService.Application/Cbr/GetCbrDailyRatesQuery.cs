using MediatR;
using Rates.BuildingBlocks.Domain;

namespace Rates.FinanceService.Application.Cbr;

/// <summary>
/// Инициирует запрос ежедневной XML-выгрузки ЦБ, её разбор и нормализацию. Запрос
/// возвращает распарсенный результат, чтобы worker (или другие вызывающие) могли
/// решить, что делать дальше.
/// </summary>
public sealed record GetCbrDailyRatesQuery : IQuery<Result<CbrDailyRates>>;

/// <summary>
/// Обработчик, который получает XML и делегирует разбор <see cref="ICbrRatesClient"/>.
/// </summary>
public sealed class GetCbrDailyRatesQueryHandler : IRequestHandler<GetCbrDailyRatesQuery, Result<CbrDailyRates>>
{
    private readonly ICbrRatesClient _client;

    public GetCbrDailyRatesQueryHandler(ICbrRatesClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<Result<CbrDailyRates>> Handle(GetCbrDailyRatesQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var rates = await _client.GetDailyRatesAsync(cancellationToken);
            return Result<CbrDailyRates>.Ok(rates);
        }
        catch (HttpRequestException ex)
        {
            return Result<CbrDailyRates>.Failure(
                Error.Unavailable("cbr_unreachable", $"CBR endpoint is unreachable: {ex.Message}"));
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<CbrDailyRates>.Failure(
                Error.Unavailable("cbr_timeout", $"CBR endpoint timed out: {ex.Message}"));
        }
        catch (InvalidOperationException ex)
        {
            return Result<CbrDailyRates>.Failure(
                Error.Unavailable("cbr_invalid_response", $"CBR returned invalid data: {ex.Message}"));
        }
    }
}
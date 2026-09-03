namespace Rates.FinanceService.Application.Cbr;

/// <summary>
/// Возвращает распарсенные ежедневные курсы из XML-выгрузки ЦБ РФ. Реализация по умолчанию
/// находится в Infrastructure и зарегистрирована как типизированный HTTP-клиент.
/// </summary>
public interface ICbrRatesClient
{
    Task<CbrDailyRates> GetDailyRatesAsync(CancellationToken cancellationToken);
}
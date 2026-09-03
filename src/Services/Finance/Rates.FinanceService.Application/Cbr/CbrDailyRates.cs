namespace Rates.FinanceService.Application.Cbr;

/// <summary>
/// Одна строка ежедневной XML-выгрузки ЦБ РФ.
/// </summary>
public sealed record CbrCurrencyEntry(
    string CbrId,
    string NumCode,
    string CharCode,
    string Name,
    int Nominal,
    decimal Value,
    decimal NormalizedRate)
{
    public string Code => CharCode;
}

public sealed record CbrDailyRates(DateOnly RateDate, IReadOnlyList<CbrCurrencyEntry> Entries)
{
    public static CbrDailyRates Empty { get; } = new(default, Array.Empty<CbrCurrencyEntry>());
}
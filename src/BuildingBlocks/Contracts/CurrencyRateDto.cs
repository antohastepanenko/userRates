namespace Rates.BuildingBlocks.Contracts;

/// <summary>
/// Публичный контракт, возвращаемый шлюзом. Стабильный wire-формат — НЕ переименовывайте поля
/// без согласования с фронтенд-клиентами.
/// </summary>
public sealed record CurrencyRateDto(
    string Code,
    string Name,
    decimal Rate,
    decimal Nominal,
    DateOnly RateDate,
    DateTimeOffset? AddedAt = null);

public sealed record CurrencyRatesResponse(DateOnly? AsOf, IReadOnlyList<CurrencyRateDto> Items)
{
    public static CurrencyRatesResponse Empty { get; } = new(null, Array.Empty<CurrencyRateDto>());
}
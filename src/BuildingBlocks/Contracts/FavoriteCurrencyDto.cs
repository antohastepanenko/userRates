namespace Rates.BuildingBlocks.Contracts;

/// <summary>
/// Wire-формат для одного кода избранной валюты. Мы раскрываем только код, никогда — идентификатор пользователя.
/// </summary>
public sealed record FavoriteCurrencyDto(string Code, DateTimeOffset AddedAt);

public sealed record AddFavoriteRequest(string Code);

public sealed record FavoriteCurrenciesResponse(IReadOnlyList<FavoriteCurrencyDto> Items)
{
    public static FavoriteCurrenciesResponse Empty { get; } = new(Array.Empty<FavoriteCurrencyDto>());
}
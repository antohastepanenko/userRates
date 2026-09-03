namespace Rates.BuildingBlocks.Infrastructure.Options;

/// <summary>
/// Опции, привязанные к секции конфигурации <c>CbrRates</c>. Содержат адрес ежедневной
/// выгрузки курсов ЦБ РФ, интервал обновления и политику мягкого отказа при недоступности
/// удалённого сервиса.
/// </summary>
public sealed class CbrOptions
{
    public const string SectionName = "CbrRates";

    public string Url { get; set; } = "http://www.cbr.ru/scripts/XML_daily.asp";

    public TimeSpan UpdateInterval { get; set; } = TimeSpan.FromHours(1);

    public bool RunOnStartup { get; set; } = true;

    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(15);

    public string UserAgent { get; set; } = "Rates.Worker/1.0";
}
using System.Globalization;
using System.Xml.Linq;
using Rates.FinanceService.Application.Cbr;

namespace Rates.FinanceService.Infrastructure.Cbr;

/// <summary>
/// Разбирает ежедневную XML-выгрузку ЦБ в агрегат <see cref="CbrDailyRates"/>.
/// </summary>
public interface ICbrRatesParser
{
    CbrDailyRates Parse(string xml);
}

public sealed class CbrRatesParser : ICbrRatesParser
{
    private static readonly CultureInfo RuCulture = CultureInfo.GetCultureInfo("ru-RU");

    public CbrDailyRates Parse(string xml)
    {
        ArgumentException.ThrowIfNullOrEmpty(xml);

        var document = XDocument.Parse(xml);
        var root = document.Root ?? throw new InvalidOperationException("CBR document has no root element.");

        var dateAttribute = root.Attribute("Date")?.Value
            ?? throw new InvalidOperationException("CBR document is missing the 'Date' attribute.");

        if (!DateTime.TryParse(dateAttribute, RuCulture, DateTimeStyles.None, out var date))
        {
            throw new InvalidOperationException($"CBR date '{dateAttribute}' could not be parsed.");
        }

        var entries = root
            .Elements("Valute")
            .Select(element => ParseValute(element, DateOnly.FromDateTime(date)))
            .ToList();

        return new CbrDailyRates(DateOnly.FromDateTime(date), entries);
    }

    private static CbrCurrencyEntry ParseValute(XElement element, DateOnly rateDate)
    {
        var cbrId = element.Attribute("ID")?.Value ?? string.Empty;
        var numCode = element.Element("NumCode")?.Value ?? string.Empty;
        var charCode = element.Element("CharCode")?.Value ?? string.Empty;
        var name = element.Element("Name")?.Value ?? string.Empty;
        var nominalText = element.Element("Nominal")?.Value ?? "0";
        var valueText = element.Element("Value")?.Value ?? "0";

        if (!int.TryParse(nominalText, NumberStyles.Integer, RuCulture, out var nominal) || nominal <= 0)
        {
            throw new InvalidOperationException(
                $"CBR entry {cbrId} '{charCode}' has invalid Nominal '{nominalText}'.");
        }

        // ЦБ РФ использует в качестве десятичного разделителя запятую — культура ru-RU
        // корректно это обрабатывает.
        if (!decimal.TryParse(valueText, NumberStyles.Number, RuCulture, out var value) || value <= 0)
        {
            throw new InvalidOperationException(
                $"CBR entry {cbrId} '{charCode}' has invalid Value '{valueText}'.");
        }

        var normalizedRate = Math.Round(value / nominal, 8, MidpointRounding.AwayFromZero);

        return new CbrCurrencyEntry(cbrId, numCode, charCode, name, nominal, value, normalizedRate);
    }
}
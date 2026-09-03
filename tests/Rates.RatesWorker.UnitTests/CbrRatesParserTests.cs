using FluentAssertions;
using Rates.FinanceService.Infrastructure.Cbr;

namespace Rates.RatesWorker.UnitTests;

public sealed class CbrRatesParserTests
{
    [Fact]
    public void Parses_valid_xml_and_normalises_rates_with_nominal()
    {
        const string xml = """
                           <?xml version="1.0" encoding="windows-1251"?>
                           <ValCurs Date="02.09.2026" name="Foreign Currency Market">
                               <Valute ID="R01235">
                                   <NumCode>840</NumCode>
                                   <CharCode>USD</CharCode>
                                   <Nominal>1</Nominal>
                                   <Name>Доллар США</Name>
                                   <Value>80,1234</Value>
                               </Valute>
                               <Valute ID="R01239">
                                   <NumCode>978</NumCode>
                                   <CharCode>EUR</CharCode>
                                   <Nominal>1</Nominal>
                                   <Name>Евро</Name>
                                   <Value>90,5</Value>
                               </Valute>
                               <Valute ID="R01010">
                                   <NumCode>156</NumCode>
                                   <CharCode>CNY</CharCode>
                                   <Nominal>10</Nominal>
                                   <Name>Китайский юань</Name>
                                   <Value>112,00</Value>
                               </Valute>
                           </ValCurs>
                           """;

        var parser = new CbrRatesParser();
        var rates = parser.Parse(xml);

        rates.RateDate.Should().Be(new DateOnly(2026, 9, 2));
        rates.Entries.Should().HaveCount(3);

        var usd = rates.Entries.Single(e => e.CharCode == "USD");
        usd.Nominal.Should().Be(1);
        usd.Value.Should().Be(80.1234m);
        usd.NormalizedRate.Should().Be(80.12340000m);

        var cny = rates.Entries.Single(e => e.CharCode == "CNY");
        cny.Nominal.Should().Be(10);
        cny.Value.Should().Be(112.00m);
        cny.NormalizedRate.Should().Be(11.20000000m);
    }

    [Fact]
    public void Throws_when_date_missing()
    {
        const string xml = """
                           <ValCurs>
                               <Valute ID="R01235">
                                   <NumCode>840</NumCode>
                                   <CharCode>USD</CharCode>
                                   <Nominal>1</Nominal>
                                   <Name>Доллар США</Name>
                                   <Value>80,1234</Value>
                               </Valute>
                           </ValCurs>
                           """;

        Action act = () => new CbrRatesParser().Parse(xml);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*missing the 'Date' attribute*");
    }

    [Fact]
    public void Throws_on_invalid_value()
    {
        const string xml = """
                           <?xml version="1.0" encoding="windows-1251"?>
                           <ValCurs Date="02.09.2026" name="Foreign Currency Market">
                               <Valute ID="R01235">
                                   <NumCode>840</NumCode>
                                   <CharCode>USD</CharCode>
                                   <Nominal>1</Nominal>
                                   <Name>Доллар США</Name>
                                   <Value>abc</Value>
                               </Valute>
                           </ValCurs>
                           """;

        Action act = () => new CbrRatesParser().Parse(xml);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*invalid Value*");
    }

    [Fact]
    public void Throws_on_invalid_nominal()
    {
        const string xml = """
                           <?xml version="1.0" encoding="windows-1251"?>
                           <ValCurs Date="02.09.2026" name="Foreign Currency Market">
                               <Valute ID="R01235">
                                   <NumCode>840</NumCode>
                                   <CharCode>USD</CharCode>
                                   <Nominal>0</Nominal>
                                   <Name>Доллар США</Name>
                                   <Value>80,1234</Value>
                               </Valute>
                           </ValCurs>
                           """;

        Action act = () => new CbrRatesParser().Parse(xml);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*invalid Nominal*");
    }
}
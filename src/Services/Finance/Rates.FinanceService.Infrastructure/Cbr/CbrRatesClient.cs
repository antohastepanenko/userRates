using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Rates.BuildingBlocks.Infrastructure.Options;
using Rates.FinanceService.Application.Cbr;

namespace Rates.FinanceService.Infrastructure.Cbr;

/// <summary>
/// Типизированный HTTP-клиент ежедневной выгрузки ЦБ. Настраивается через <see cref="CbrOptions"/>,
/// чтобы в тестах worker мог указывать на мок, а в production — на реальный эндпоинт.
/// </summary>
public sealed class CbrRatesClient : ICbrRatesClient
{
    private static readonly object EncodingInitLock = new();
    private static bool _encodingsRegistered;

    private readonly HttpClient _http;
    private readonly ICbrRatesParser _parser;
    private readonly CbrOptions _options;
    private readonly ILogger<CbrRatesClient> _logger;

    public CbrRatesClient(HttpClient http, ICbrRatesParser parser, Microsoft.Extensions.Options.IOptions<CbrOptions> options, ILogger<CbrRatesClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;

        EnsureEncodingsRegistered();

        if (_http.BaseAddress is null && !string.IsNullOrWhiteSpace(_options.Url))
        {
            _http.BaseAddress = new Uri(_options.Url);
        }

        if (_http.Timeout == TimeSpan.Zero || _http.Timeout > _options.HttpTimeout)
        {
            _http.Timeout = _options.HttpTimeout;
        }

        if (_http.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);
        }

        _http.DefaultRequestHeaders.Accept.Clear();
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
        _http.DefaultRequestHeaders.AcceptCharset.Clear();
        _http.DefaultRequestHeaders.AcceptCharset.ParseAdd("utf-8");
    }

    public async Task<CbrDailyRates> GetDailyRatesAsync(CancellationToken cancellationToken)
    {
        var url = _options.Url ?? throw new InvalidOperationException("CbrRates:Url is not configured.");
        _logger.LogInformation("Fetching CBR daily rates from {Url}", url);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("identity", 1.0));

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var charset = response.Content.Headers.ContentType?.CharSet;
        var xml = TryDecode(bytes, charset) ?? Encoding.UTF8.GetString(bytes);

        var parsed = _parser.Parse(xml);
        _logger.LogInformation(
            "Fetched {Count} CBR entries for {RateDate}",
            parsed.Entries.Count,
            parsed.RateDate);

        return parsed;
    }

    private static void EnsureEncodingsRegistered()
    {
        if (_encodingsRegistered)
        {
            return;
        }

        lock (EncodingInitLock)
        {
            if (_encodingsRegistered)
            {
                return;
            }

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _encodingsRegistered = true;
        }
    }

    private static string? TryDecode(byte[] bytes, string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset))
        {
            return null;
        }

        try
        {
            return Encoding.GetEncoding(charset).GetString(bytes);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
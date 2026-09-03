using Microsoft.AspNetCore.Http;

namespace Rates.BuildingBlocks.Infrastructure.Web;

/// <summary>
/// Считывает (или генерирует) заголовок X-Correlation-Id и пробрасывает его в запросе
/// и ответе. Полезно для склейки логов между шлюзом и нижестоящими сервисами.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    private const int MaxLength = 128;

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var existing)
            && existing.Count == 1
            && IsValid(existing[0])
            ? existing[0]!
            : Guid.NewGuid().ToString("N");

        // Трансформ YARP «RequestHeadersCopy» копирует это каноническое значение дальше по цепочке.
        context.Request.Headers[HeaderName] = correlationId;
        context.Items[HeaderName] = correlationId;
        context.Response.OnStarting(() =>
        {
            // Нижестоящий сервис не должен иметь возможность подменить корреляционный id шлюза.
            context.Response.Headers.Remove(HeaderName);
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        await _next(context);
    }

    public static string GetCorrelationId(HttpContext context) =>
        context.Items.TryGetValue(HeaderName, out var value) && value is string s
            ? s
            : context.TraceIdentifier;

    private static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxLength)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!(char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or ':'))
            {
                return false;
            }
        }

        return true;
    }
}

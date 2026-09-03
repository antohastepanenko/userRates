using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Rates.BuildingBlocks.Domain;

namespace Rates.BuildingBlocks.Infrastructure.Web;

/// <summary>
/// Преобразует значения <see cref="Error"/> из application-слоя в ответы формата
/// RFC 7807 problem details. Каждый сервис регистрирует этот middleware однократно
/// через <c>UseRatesProblemDetails</c> в Program.cs.
/// </summary>
public sealed class ProblemDetailsMiddleware
{
    private static readonly IReadOnlyDictionary<ErrorType, int> StatusCodeMap = new Dictionary<ErrorType, int>
    {
        [ErrorType.Validation] = StatusCodes.Status400BadRequest,
        [ErrorType.NotFound] = StatusCodes.Status404NotFound,
        [ErrorType.Conflict] = StatusCodes.Status409Conflict,
        [ErrorType.Unauthorized] = StatusCodes.Status401Unauthorized,
        [ErrorType.Forbidden] = StatusCodes.Status403Forbidden,
        [ErrorType.Unavailable] = StatusCodes.Status503ServiceUnavailable,
        [ErrorType.Unexpected] = StatusCodes.Status500InternalServerError,
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ProblemDetailsMiddleware> _logger;

    public ProblemDetailsMiddleware(RequestDelegate next, ILogger<ProblemDetailsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in request {Path}", context.Request.Path);
            if (context.Response.HasStarted)
            {
                throw;
            }

            await WriteAsync(
                context,
                Error.Unexpected("internal_error", "An unexpected error occurred."),
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    public static async Task WriteAsync(HttpContext context, Error error, int? statusCode = null)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        var status = statusCode ?? StatusCodeMap[error.Type];
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var problem = new
        {
            type = $"https://rates.local/errors/{error.Code}",
            title = error.Code,
            status,
            detail = error.Message,
            traceId = context.TraceIdentifier,
            correlationId = CorrelationIdMiddleware.GetCorrelationId(context),
        };

        await context.Response.WriteAsJsonAsync(problem);
    }
}
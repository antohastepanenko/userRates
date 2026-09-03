using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Rates.BuildingBlocks.Domain;

namespace Rates.BuildingBlocks.Infrastructure.Web;

/// <summary>
/// Общее преобразование ошибок application-слоя в ответы RFC 7807. Контроллеры используют
/// этот помощник вместо того, чтобы дублировать switch по статус-кодам в каждом эндпоинте.
/// </summary>
public static class ProblemDetailsResult
{
    public static IActionResult From(ControllerBase controller, Error error)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(error);

        var status = StatusCodeFor(error.Type);
        return new ObjectResult(new
        {
            type = $"https://rates.local/errors/{error.Code}",
            title = error.Code,
            status,
            detail = error.Message,
            traceId = controller.HttpContext.TraceIdentifier,
        })
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };
    }

    public static int StatusCodeFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
        ErrorType.Unexpected => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status500InternalServerError,
    };
}
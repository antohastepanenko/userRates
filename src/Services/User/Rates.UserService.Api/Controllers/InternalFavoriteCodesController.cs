using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rates.BuildingBlocks.Contracts;
using Rates.BuildingBlocks.Domain;
using Rates.BuildingBlocks.Infrastructure.Security;
using Rates.BuildingBlocks.Infrastructure.Web;
using Rates.UserService.Application.Favorites;

namespace Rates.UserService.Api.Controllers;

/// <summary>
/// Межсервисный эндпоинт, используемый Finance-сервисом. Не проксируется через шлюз
/// и требует общий внутренний служебный токен.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("internal/v1/users/{userId:guid}/favorite-codes")]
public sealed class InternalFavoriteCodesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IInternalServiceTokenValidator _tokenValidator;

    public InternalFavoriteCodesController(
        IMediator mediator,
        IInternalServiceTokenValidator tokenValidator)
    {
        _mediator = mediator;
        _tokenValidator = tokenValidator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAsync(
        Guid userId,
        [FromHeader(Name = InternalServiceHeaders.TokenHeaderName)] string? serviceToken,
        CancellationToken cancellationToken)
    {
        if (!_tokenValidator.IsValid(serviceToken))
        {
            return ProblemFrom(Error.Unauthorized(
                "invalid_internal_service_token",
                "A valid internal service token is required."));
        }

        var result = await _mediator.Send(new GetFavoriteCodesForUserQuery(userId), cancellationToken);
        if (result.IsFailure)
        {
            return ProblemFrom(result.Error);
        }

        return Ok(new InternalFavoriteCodesResponse(userId, result.Value.ToArray()));
    }

    private IActionResult ProblemFrom(Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status400BadRequest,
        };

        return new ObjectResult(new
        {
            type = $"https://rates.local/errors/{error.Code}",
            title = error.Code,
            status,
            detail = error.Message,
            traceId = HttpContext.TraceIdentifier,
            correlationId = CorrelationIdMiddleware.GetCorrelationId(HttpContext),
        })
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };
    }
}

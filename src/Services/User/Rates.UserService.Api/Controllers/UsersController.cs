using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rates.BuildingBlocks.Contracts;
using Rates.BuildingBlocks.Domain;
using Rates.UserService.Application.Favorites;
using Rates.UserService.Application.Users;

namespace Rates.UserService.Api.Controllers;

/// <summary>Эндпоинты под <c>/api/v1/users/me/**</c>.</summary>
[ApiController]
[Authorize]
[Route("api/v1/users/me")]
public sealed class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetCurrentUserQuery(), cancellationToken);
        if (result.IsFailure)
        {
            return ProblemFrom(result.Error);
        }

        var view = result.Value;
        return Ok(new CurrentUserResponse(view.Id, view.Name, view.CreatedAt));
    }

    [HttpGet("favorites")]
    public async Task<IActionResult> ListFavoritesAsync(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetFavoriteCurrenciesQuery(), cancellationToken);
        if (result.IsFailure)
        {
            return ProblemFrom(result.Error);
        }

        var items = result.Value
            .Select(f => new FavoriteCurrencyDto(f.Code, f.AddedAt))
            .ToArray();

        return Ok(new FavoriteCurrenciesResponse(items));
    }

    [HttpPut("favorites/{code}")]
    public async Task<IActionResult> AddFavoriteAsync(string code, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new AddFavoriteCurrencyCommand(code), cancellationToken);
        return result.IsFailure
            ? ProblemFrom(result.Error)
            : NoContent();
    }

    [HttpDelete("favorites/{code}")]
    public async Task<IActionResult> RemoveFavoriteAsync(string code, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RemoveFavoriteCurrencyCommand(code), cancellationToken);
        return result.IsFailure
            ? ProblemFrom(result.Error)
            : NoContent();
    }

    private IActionResult ProblemFrom(Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
            ErrorType.Unexpected => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status400BadRequest,
        };

        return new ObjectResult(new
        {
            type = $"https://rates.local/errors/{error.Code}",
            title = error.Code,
            status,
            detail = error.Message,
            traceId = HttpContext.TraceIdentifier,
        })
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };
    }
}
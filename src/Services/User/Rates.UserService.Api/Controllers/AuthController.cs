using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rates.BuildingBlocks.Contracts;
using Rates.BuildingBlocks.Domain;
using Rates.UserService.Application.Auth;

namespace Rates.UserService.Api.Controllers;

/// <summary>
/// Публичные эндпоинты аутентификации. Шлюз проксирует сюда <c>/api/v1/auth/**</c>.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
public sealed class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("register")]
    public async Task<IActionResult> RegisterAsync([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RegisterUserCommand(request.Name, request.Password), cancellationToken);

        if (result.IsFailure)
        {
            return ProblemFrom(result.Error);
        }

        var user = result.Value;
        return Created(
            $"/api/v1/users/me",
            new TokenResponse(
                AccessToken: user.Tokens.AccessToken,
                RefreshToken: user.Tokens.RefreshToken,
                AccessTokenExpiresAt: user.Tokens.AccessTokenExpiresAt,
                RefreshTokenExpiresAt: user.Tokens.RefreshTokenExpiresAt));
    }

    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new LoginUserCommand(request.Name, request.Password), cancellationToken);
        if (result.IsFailure)
        {
            return ProblemFrom(result.Error);
        }

        var user = result.Value;
        return Ok(new TokenResponse(
            user.Tokens.AccessToken,
            user.Tokens.RefreshToken,
            user.Tokens.AccessTokenExpiresAt,
            user.Tokens.RefreshTokenExpiresAt));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshAsync([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RefreshTokenCommand(request.RefreshToken), cancellationToken);
        if (result.IsFailure)
        {
            return ProblemFrom(result.Error);
        }

        var user = result.Value;
        return Ok(new TokenResponse(
            user.Tokens.AccessToken,
            user.Tokens.RefreshToken,
            user.Tokens.AccessTokenExpiresAt,
            user.Tokens.RefreshTokenExpiresAt));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> LogoutAsync([FromBody] LogoutRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new LogoutCommand(request.RefreshToken), cancellationToken);
        if (result.IsFailure)
        {
            return ProblemFrom(result.Error);
        }

        return NoContent();
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
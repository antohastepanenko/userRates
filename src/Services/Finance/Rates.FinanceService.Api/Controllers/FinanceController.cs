using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rates.BuildingBlocks.Contracts;
using Rates.BuildingBlocks.Domain;
using Rates.BuildingBlocks.Infrastructure.Web;
using Rates.FinanceService.Application.Currencies;

namespace Rates.FinanceService.Api.Controllers;

/// <summary>
/// Публичные finance-эндпоинты. Идентичность пользователя берётся из валидированного JWT;
/// клиенты не могут передавать произвольный user id.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/finance/currencies")]
public sealed class FinanceController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<FinanceController> _logger;

    public FinanceController(IMediator mediator, ILogger<FinanceController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUserRatesAsync(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetUserCurrencyRatesQuery(), cancellationToken);
        if (result.IsFailure)
        {
            return ProblemFrom(result.Error);
        }

        if (result.Value.MissingCodes.Count > 0)
        {
            _logger.LogWarning(
                "No current rates found for favorite currency codes: {Codes}",
                string.Join(", ", result.Value.MissingCodes));
        }

        var response = new CurrencyRatesResponse(result.Value.AsOf, result.Value.Items);
        return Ok(response);
    }

    private IActionResult ProblemFrom(Error error) =>
        ProblemDetailsResult.From(this, error);
}

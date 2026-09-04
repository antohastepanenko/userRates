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
[Route("api/v1/finance")]
public sealed class FinanceController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<FinanceController> _logger;

    public FinanceController(IMediator mediator, ILogger<FinanceController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Полный каталог валют с последним известным курсом по каждой. Используется UI
    /// для построения выпадающего списка доступных валют.
    /// </summary>
    [HttpGet("currencies")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllCurrenciesAsync(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetLatestCurrencyRatesQuery(), cancellationToken);
        if (result.IsFailure)
        {
            return ProblemFrom(result.Error);
        }

        var response = new CurrencyRatesResponse(result.Value.AsOf, result.Value.Items);
        return Ok(response);
    }

    /// <summary>
    /// Курсы только по избранным валютам текущего пользователя. Список избранного
    /// запрашивается в UserService; коды, отсутствующие в каталоге, возвращаются в
    /// <c>missingCodes</c>, чтобы UI мог их визуально выделить.
    /// </summary>
    [HttpGet("me/favorites")]
    public async Task<IActionResult> GetUserFavoriteRatesAsync(CancellationToken cancellationToken)
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

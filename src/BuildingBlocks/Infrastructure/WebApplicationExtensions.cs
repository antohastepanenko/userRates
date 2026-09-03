using Microsoft.AspNetCore.Builder;
using Rates.BuildingBlocks.Infrastructure.Web;

namespace Rates.BuildingBlocks.Infrastructure;

/// <summary>
/// Регистрации конвейера, которые должен выполнять любой HTTP-сервис: первым — middleware
/// корреляционного идентификатора, последним — middleware problem details, чтобы любое
/// необработанное исключение превращалось в корректный ответ.
/// </summary>
public static class WebApplicationExtensions
{
    public static WebApplication UseRatesCommonPipeline(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<ProblemDetailsMiddleware>();
        return app;
    }
}
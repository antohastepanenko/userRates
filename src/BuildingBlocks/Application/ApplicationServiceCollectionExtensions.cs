using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Rates.BuildingBlocks.Application;

/// <summary>
/// Добавляет в DI-контейнер MediatR, валидацию и поведения конвейера. Каждый сервис
/// подключает MediatR с сборками своего application/domain-слоя, чтобы обработчики
/// медиатора обнаруживались в пределах границ сервиса.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует MediatR, поведение конвейера валидации и поведение конвейера логирования.
    /// </summary>
    /// <param name="services">Коллекция сервисов, которую нужно расширить.</param>
    /// <param name="assemblies">Сборки, содержащие реализации <see cref="IRequestHandler{TRequest,TResponse}"/>.</param>
    public static IServiceCollection AddRatesApplication(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (assemblies is null || assemblies.Length == 0)
        {
            throw new ArgumentException(
                "At least one assembly must be supplied so MediatR can find handlers.",
                nameof(assemblies));
        }

        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssemblies(assemblies);
        });

        services.AddValidatorsFromAssemblies(assemblies, includeInternalTypes: true);

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

        return services;
    }
}
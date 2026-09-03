namespace Rates.FinanceService.Application;

/// <summary>
/// Тип-якорь, который используется в
/// <see cref="Rates.BuildingBlocks.Application.ApplicationServiceCollectionExtensions.AddRatesApplication(Microsoft.Extensions.DependencyInjection.IServiceCollection, System.Reflection.Assembly[])"/>
/// для обнаружения обработчиков без явной ссылки на конкретный тип.
/// </summary>
public sealed class AssemblyMarker;
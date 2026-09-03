using MediatR;

namespace Rates.BuildingBlocks.Domain;

/// <summary>
/// Маркерный интерфейс для сценариев записи (команд).
/// </summary>
public interface ICommand<out TResponse> : IRequest<TResponse>
{
}

/// <summary>
/// Маркерный интерфейс для команд, которые не возвращают содержательного ответа.
/// </summary>
public interface ICommand : ICommand<Unit>
{
}

/// <summary>
/// Маркерный интерфейс для сценариев чтения (запросов).
/// </summary>
public interface IQuery<out TResponse> : IRequest<TResponse>
{
}
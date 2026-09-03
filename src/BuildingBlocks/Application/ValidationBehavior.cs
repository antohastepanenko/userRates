using FluentValidation;
using MediatR;
using Rates.BuildingBlocks.Domain;

namespace Rates.BuildingBlocks.Application;

/// <summary>
/// Поведение в конвейере MediatR, которое запускает зарегистрированные экземпляры
/// <see cref="IValidator{TRequest}"/> до выполнения обработчика. Ошибки валидации преобразуются
/// в <see cref="Result{T}"/> со статусом Failure, поэтому обработчик может оставаться синхронным.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var failures = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var errors = failures
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (errors.Count == 0)
        {
            return await next();
        }

        var error = Error.Validation("validation_failed", string.Join("; ", errors.Select(e => e.ErrorMessage)));

        if (typeof(TResponse) == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(error);
        }

        if (typeof(TResponse).IsGenericType && typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
        {
            var genericArgument = typeof(TResponse).GetGenericArguments()[0];
            var failureMethod = typeof(Result<>)
                .MakeGenericType(genericArgument)
                .GetMethod(nameof(Result<object>.Failure), new[] { typeof(Error) });

            return (TResponse)failureMethod!.Invoke(null, new object[] { error })!;
        }

        // Резервный вариант для обработчиков, которые не используют Result<T>: выбрасываем
        // ValidationException, чтобы вызывающий код понял, что запрос отклонён.
        throw new ValidationException(errors);
    }
}
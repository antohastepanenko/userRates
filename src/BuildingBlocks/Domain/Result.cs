namespace Rates.BuildingBlocks.Domain;

/// <summary>
/// Дискриминированный тип результата. Либо <see cref="Ok(T)"/> при успехе, либо
/// <see cref="Failure"/> — когда вызывающий код должен вернуть problem-ответ
/// (ошибка валидации, не найдено, конфликт и т. п.).
/// </summary>
/// <remarks>
/// Тип намеренно сделан минималистичным, чтобы обработчики могли компоновать сценарии
/// использования без исключений.
/// </remarks>
public readonly struct Result<T>
{
    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T Value { get; }

    public Error Error { get; }

    private Result(bool isSuccess, T value, Error error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Ok(T value) => new(true, value, Error.None);

    public static Result<T> Failure(Error error) => new(false, default!, error);

    public static implicit operator Result<T>(T value) => Ok(value);
}

/// <summary>Результат без значения для команд, которые не возвращают данные.</summary>
public readonly struct Result
{
    public bool IsSuccess { get; }

    public Error Error { get; }

    private Result(bool isSuccess, Error error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success { get; } = new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static implicit operator Result(Error error) => Failure(error);
}
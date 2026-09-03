namespace Rates.BuildingBlocks.Domain;

/// <summary>
/// Стабильные коды ошибок, которые однозначно отображаются на типы HTTP problem details в API-слое.
/// </summary>
public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
    Unexpected,
    Unavailable,
}

/// <summary>
/// Содержит стабильный код, человекочитаемое сообщение и категорию ошибки. Возвращается
/// из сценариев использования, чтобы вызывающий код мог преобразовать их в HTTP-ответы
/// без зависимости от Infrastructure-слоя.
/// </summary>
public sealed record Error(ErrorType Type, string Code, string Message)
{
    public static Error None { get; } = new(ErrorType.Validation, string.Empty, string.Empty);

    public static Error NotFound(string code, string message) => new(ErrorType.NotFound, code, message);
    public static Error Conflict(string code, string message) => new(ErrorType.Conflict, code, message);
    public static Error Validation(string code, string message) => new(ErrorType.Validation, code, message);
    public static Error Unauthorized(string code, string message) => new(ErrorType.Unauthorized, code, message);
    public static Error Forbidden(string code, string message) => new(ErrorType.Forbidden, code, message);
    public static Error Unexpected(string code, string message) => new(ErrorType.Unexpected, code, message);
    public static Error Unavailable(string code, string message) => new(ErrorType.Unavailable, code, message);
}
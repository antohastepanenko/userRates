using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Rates.BuildingBlocks.Infrastructure.Options;

namespace Rates.BuildingBlocks.Infrastructure.Security;

public static class InternalServiceHeaders
{
    public const string TokenHeaderName = "X-Internal-Service-Token";
}

/// <summary>
/// Проверяет общий служебный токен за постоянное время. Валидатор намеренно изолирован
/// от ASP.NET-схем аутентификации, чтобы он мог защищать внутренние действия контроллеров
/// без второго публичного потока аутентификации.
/// </summary>
public interface IInternalServiceTokenValidator
{
    bool IsValid(string? suppliedToken);
}

public sealed class InternalServiceTokenValidator : IInternalServiceTokenValidator
{
    private readonly string _expectedToken;

    public InternalServiceTokenValidator(IOptions<InternalServiceOptions> options)
    {
        _expectedToken = options.Value.SharedToken ?? string.Empty;
    }

    public bool IsValid(string? suppliedToken)
    {
        if (string.IsNullOrEmpty(_expectedToken) || string.IsNullOrEmpty(suppliedToken))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(_expectedToken);
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedToken);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }
}

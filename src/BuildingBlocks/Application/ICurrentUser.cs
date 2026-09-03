using System.Security.Claims;

namespace Rates.BuildingBlocks.Application;

/// <summary>
/// Абстракция над текущим пользователем. Реализация находится в Infrastructure
/// (<c>HttpContextCurrentUser</c> для HTTP-сервисов).
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    string? Name { get; }

    bool IsAuthenticated { get; }

    IReadOnlyCollection<Claim> Claims { get; }
}
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Rates.BuildingBlocks.Application;

namespace Rates.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// Читает JWT-утверждения из текущего <see cref="HttpContext"/>, чтобы обработчики могли
/// получить <c>UserId</c> без привязки к HTTP-инфраструктуре.
/// </summary>
public sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public Guid? UserId
    {
        get
        {
            var value = FindClaim(ClaimTypes.NameIdentifier)?.Value
                ?? FindClaim("sub")?.Value;

            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Name => FindClaim(ClaimTypes.Name)?.Value ?? FindClaim("name")?.Value;

    public IReadOnlyCollection<Claim> Claims =>
        _httpContextAccessor.HttpContext?.User.Claims
            .ToArray()
        ?? Array.Empty<Claim>();

    private Claim? FindClaim(string type)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user is null ? null : user.FindFirst(type);
    }
}
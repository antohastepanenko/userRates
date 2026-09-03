using Rates.UserService.Application;

namespace Rates.UserService.Infrastructure.Auth;

/// <summary>
/// Адаптер, делегирующий хэшер из BuildingBlocks на основе BCrypt. Хранение адаптера
/// в Infrastructure не позволяет пространству имён <c>Rates.BuildingBlocks.Infrastructure.Security</c>
/// «протечь» в Application-слой.
/// </summary>
public sealed class PasswordHasherAdapter : IPasswordHasher
{
    private readonly Rates.BuildingBlocks.Infrastructure.Security.IPasswordHasher _inner;

    public PasswordHasherAdapter(Rates.BuildingBlocks.Infrastructure.Security.IPasswordHasher inner)
    {
        _inner = inner;
    }

    public string Hash(string password) => _inner.Hash(password);

    public bool Verify(string password, string hash) => _inner.Verify(password, hash);
}
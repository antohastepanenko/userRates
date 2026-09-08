namespace Rates.UserService.Application.Auth;

/// <summary>
/// Адаптер от доменного <see cref="IPasswordHasher"/> (Application-слой)
/// к инфраструктурному <see cref="Rates.BuildingBlocks.Infrastructure.Security.IPasswordHasher"/>.
/// Реализация регистрируется в Infrastructure.
/// </summary>
public sealed class PasswordHasherAdapter(Rates.BuildingBlocks.Infrastructure.Security.IPasswordHasher inner)
    : IPasswordHasher
{
    public string Hash(string password) => inner.Hash(password);

    public bool Verify(string password, string hash) => inner.Verify(password, hash);
}

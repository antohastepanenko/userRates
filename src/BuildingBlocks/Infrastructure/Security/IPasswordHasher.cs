namespace Rates.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// Контракт хэширования паролей. По умолчанию используется BCrypt (cost ≥ 11); подойдёт
/// и любая другая KDF, при условии что её хэш сохраняется в колонку БД «как есть».
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}
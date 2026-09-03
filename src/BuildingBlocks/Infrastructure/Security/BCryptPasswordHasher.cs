namespace Rates.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// Реализация на основе BCrypt. Work factor 12 — разумное значение по умолчанию для
/// интерактивных входов на обычном «массовом» оборудовании (актуально для 2024 года).
/// </summary>
public sealed class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // BCrypt не может прочитать формат хэша — обрабатываем это как ошибку верификации,
            // а не даём исключению выйти на уровень API.
            return false;
        }
    }
}
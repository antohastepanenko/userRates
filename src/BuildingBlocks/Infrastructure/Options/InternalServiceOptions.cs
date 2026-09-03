namespace Rates.BuildingBlocks.Infrastructure.Options;

/// <summary>
/// Общий учётный токен для межсервисных вызовов. Должен задаваться провайдером секретов /
/// переменной окружения и никогда не коммититься в репозиторий как боевой продовый секрет.
/// </summary>
public sealed class InternalServiceOptions
{
    public const string SectionName = "InternalServices";

    public string SharedToken { get; set; } = string.Empty;
}

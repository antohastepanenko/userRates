namespace Rates.UserService.IntegrationTests;

/// <summary>
/// Заглушка этапа 2. На этапах 5/8 будут добавлены реальные интеграционные тесты
/// на базе WebApplicationFactory + Testcontainers для PostgreSQL.
/// </summary>
public sealed class SmokeTests
{
    [Fact(Skip = "Awaiting Stage 5: WebApplicationFactory + Postgres fixture")]
    public void Placeholder() => Assert.True(true);
}
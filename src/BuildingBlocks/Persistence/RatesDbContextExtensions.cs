using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;

namespace Rates.BuildingBlocks.Persistence;

/// <summary>
/// Хелпер, который регистрирует <see cref="RatesDbContext"/> с согласованными
/// опциями Npgsql: имена таблиц и колонок приводятся к snake_case, история миграций
/// хранится в схеме <c>public</c>.
/// </summary>
public static class RatesDbContextExtensions
{
    /// <summary>
    /// Применяет настройки Npgsql к <see cref="DbContextOptionsBuilder{TContext}"/> для
    /// <see cref="RatesDbContext"/>. Использование не-generic-параметра позволяет вызывать
    /// метод из лямбды, передаваемой в <c>AddDbContext&lt;RatesDbContext&gt;</c>, где
    /// компилятор не в состоянии вывести generic-тип у <c>DbContextOptionsBuilder</c>.
    /// </summary>
    public static DbContextOptionsBuilder UseRatesNpgsql(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return optionsBuilder.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "public"));
    }
}

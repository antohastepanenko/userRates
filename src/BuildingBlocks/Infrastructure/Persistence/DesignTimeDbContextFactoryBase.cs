using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Rates.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Вспомогательная фабрика, которую использует <c>dotnet ef</c> для проектных миграций.
/// Каждый сервис предоставляет конкретную фабрику, указывающую на свой DbContext.
/// </summary>
public abstract class DesignTimeDbContextFactoryBase<TContext> : IDesignTimeDbContextFactory<TContext>
    where TContext : DbContext
{
    protected abstract string DefaultConnectionString { get; }

    protected abstract TContext CreateContext(DbContextOptionsBuilder<TContext> builder);

    public TContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        var connectionString = Environment.GetEnvironmentVariable("EF_CONNECTION_STRING") ?? DefaultConnectionString;
        ConfigureProvider(builder, connectionString);
        return CreateContext(builder);
    }

    protected abstract void ConfigureProvider(DbContextOptionsBuilder<TContext> builder, string connectionString);
}
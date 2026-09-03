using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Rates.BuildingBlocks.Domain;

namespace Rates.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Маркер для корневых агрегатов, чтобы общий перехватчик SaveChanges мог отправлять
/// поднятые доменные события во внутрипроцессную шину.
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}

/// <summary>
/// Базовый <see cref="DbContext"/>, который:
/// <list type="bullet">
///   <item>настраивает соглашение об именовании snake_case для таблиц и колонок PostgreSQL,</item>
///   <item>подавляет предупреждение EF <c>MultipleCollectionInclude</c>,</item>
///   <item>предоставляет <see cref="IUnitOfWork"/> через <see cref="SaveChangesAsync"/>.</item>
/// </list>
/// </summary>
public abstract class RatesDbContext : DbContext, IUnitOfWork
{
    [SuppressMessage("Usage", "EF1001:Internal EF Core API usage",
        Justification = "Convention builder is stable across EF Core 8.x; revisit on major upgrades.")]
    protected RatesDbContext(DbContextOptions options)
        : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        // Идиоматичный способ EF Core 8: оставляем имена свойств как есть, преобразуем их
        // в snake_case на уровне SQL.
        configurationBuilder.Properties<string>().HaveColumnType("text");
    }
}
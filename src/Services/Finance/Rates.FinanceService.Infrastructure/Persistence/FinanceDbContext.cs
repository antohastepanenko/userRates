using Microsoft.EntityFrameworkCore;
using Rates.BuildingBlocks.Infrastructure.Persistence;
using Rates.FinanceService.Domain;

namespace Rates.FinanceService.Infrastructure.Persistence;

/// <summary>
/// EF-контекст, владеющий finance-схемой: <c>currency</c>. Имя схемы — <c>finance</c>,
/// чтобы она могла сосуществовать с identity-схемой без конфликтов.
/// </summary>
public sealed class FinanceDbContext : RatesDbContext
{
    public const string SchemaName = "finance";

    public FinanceDbContext(DbContextOptions<FinanceDbContext> options)
        : base(options)
    {
    }

    public DbSet<Currency> Currencies => Set<Currency>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(SchemaName);

        ConfigureCurrency(modelBuilder.Entity<Currency>());

        base.OnModelCreating(modelBuilder);
    }

    private static void ConfigureCurrency(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Currency> builder)
    {
        builder.ToTable("currency");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Code)
            .HasColumnName("code")
            .HasMaxLength(3)
            .IsRequired();
        builder.Property(c => c.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(c => c.Rate)
            .HasColumnName("rate")
            .HasColumnType("numeric(18,8)")
            .IsRequired();
        builder.Property(c => c.Nominal)
            .HasColumnName("nominal")
            .HasColumnType("numeric(18,4)")
            .IsRequired();
        builder.Property(c => c.RateDate).HasColumnName("rate_date").IsRequired();
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(c => c.Code).IsUnique().HasDatabaseName("ix_currency_code");
        builder.HasIndex(c => c.RateDate).HasDatabaseName("ix_currency_rate_date");
    }
}
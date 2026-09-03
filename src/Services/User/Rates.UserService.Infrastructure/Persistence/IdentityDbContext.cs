using Microsoft.EntityFrameworkCore;
using Rates.BuildingBlocks.Infrastructure.Persistence;
using Rates.UserService.Domain;

namespace Rates.UserService.Infrastructure.Persistence;

/// <summary>
/// EF-контекст, владеющий схемой идентификации: <c>user</c>, <c>user_favorite_currency</c>,
/// <c>refresh_token</c>. Таблицы физически названы в snake_case, SQL-схема — <c>identity</c>,
/// чтобы она могла сосуществовать со схемой <c>finance</c>.
/// </summary>
public sealed class IdentityDbContext : RatesDbContext
{
    public const string SchemaName = "identity";

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<UserFavoriteCurrency> UserFavoriteCurrencies => Set<UserFavoriteCurrency>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.HasPostgresExtension("uuid-ossp");

        ConfigureUser(modelBuilder.Entity<User>());
        ConfigureFavorite(modelBuilder.Entity<UserFavoriteCurrency>());
        ConfigureRefreshToken(modelBuilder.Entity<RefreshToken>());

        base.OnModelCreating(modelBuilder);
    }

    private static void ConfigureUser(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<User> builder)
    {
        builder.ToTable("user");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(255)
            .IsRequired();
        builder.Property(u => u.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(u => u.Name).IsUnique().HasDatabaseName("ix_user_name");

        // Не включаем авто-обнаружение изменений по графу навигаций: изменения в Избранном
        // отслеживаются напрямую через UserFavoriteCurrency. Это предотвращает исключение
        // конкурентности «0 rows affected», когда меняется только коллекция избранного.
        builder.Ignore("Favorites");
        builder.Ignore("_favorites");
    }

    private static void ConfigureFavorite(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<UserFavoriteCurrency> builder)
    {
        builder.ToTable("user_favorite_currency");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id");
        builder.Property(f => f.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(f => f.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(3)
            .IsRequired();
        builder.Property(f => f.AddedAt).HasColumnName("added_at").IsRequired();

        builder.HasIndex(f => new { f.UserId, f.CurrencyCode })
            .IsUnique()
            .HasDatabaseName("ix_user_favorite_currency_user_code");

        // База-на-сервис: внешнего ключа на finance.currency нет, потому что они находятся
        // в разных базах данных.
    }

    private static void ConfigureRefreshToken(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_token");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(t => t.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(255)
            .IsRequired();
        builder.Property(t => t.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.RevokedAt).HasColumnName("revoked_at");
        builder.Property(t => t.ReplacedById).HasColumnName("replaced_by_id");

        builder.HasIndex(t => t.TokenHash).IsUnique().HasDatabaseName("ix_refresh_token_token_hash");
        builder.HasIndex(t => t.UserId).HasDatabaseName("ix_refresh_token_user_id");
    }
}
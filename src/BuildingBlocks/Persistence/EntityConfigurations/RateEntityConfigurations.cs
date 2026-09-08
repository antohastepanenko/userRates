using Microsoft.EntityFrameworkCore;
using Rates.FinanceService.Domain;
using Rates.UserService.Domain;

namespace Rates.BuildingBlocks.Persistence.EntityConfigurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<User> builder)
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

        builder.HasMany<UserFavoriteCurrency>()
            .WithOne()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany<RefreshToken>()
            .WithOne()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UserFavoriteCurrencyConfiguration : IEntityTypeConfiguration<UserFavoriteCurrency>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<UserFavoriteCurrency> builder)
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

        // Избранное ссылается на каталог валют — общая БД позволяет объявить FK на уровне БД.
        // При попытке добавить несуществующий код Postgres вернёт ошибку 23503.
        builder.HasOne<Currency>()
            .WithMany()
            .HasForeignKey(f => f.CurrencyCode)
            .HasPrincipalKey(c => c.Code)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<RefreshToken> builder)
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

internal sealed class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Currency> builder)
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

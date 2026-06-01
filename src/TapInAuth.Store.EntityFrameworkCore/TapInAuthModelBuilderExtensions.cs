using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TapInAuth.Store.EntityFrameworkCore.Entities;

namespace TapInAuth.Store.EntityFrameworkCore;

/// <summary>
/// Extension methods to register TapInAuth entities on the host's <see cref="DbContext"/>.
/// Call <see cref="ApplyTapInAuthConfiguration"/> inside <see cref="DbContext.OnModelCreating"/>.
/// </summary>
public static class TapInAuthModelBuilderExtensions
{
    // SQLite cannot ORDER BY a DateTimeOffset column natively. Storing all of TapInAuth's
    // timestamps as Unix milliseconds (long) makes ordering, range queries, and indexing
    // work on every supported provider (SQLite, SQL Server, PostgreSQL, MySQL) at the cost
    // of the column not being a "real" datetimeoffset in the DB schema. For an auth library
    // where we never report on these columns externally, that trade is fine.
    private static readonly ValueConverter<DateTimeOffset, long> _dtoConverter = new(
        v => v.ToUnixTimeMilliseconds(),
        v => DateTimeOffset.FromUnixTimeMilliseconds(v));

    private static readonly ValueConverter<DateTimeOffset?, long?> _dtoNullableConverter = new(
        v => v == null ? (long?)null : v.Value.ToUnixTimeMilliseconds(),
        v => v == null ? (DateTimeOffset?)null : DateTimeOffset.FromUnixTimeMilliseconds(v.Value));

    /// <summary>Register TapInAuth's four tables with their tenant-aware indexes.</summary>
    public static ModelBuilder ApplyTapInAuthConfiguration(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<TapInAuthUserEntity>(b =>
        {
            b.ToTable("TapInAuthUsers");
            b.HasKey(x => x.Id);
            b.Property(x => x.TenantId).IsRequired().HasMaxLength(128);
            b.Property(x => x.Email).IsRequired().HasMaxLength(256);
            b.Property(x => x.DisplayName).HasMaxLength(256);
            b.Property(x => x.CreatedAt).HasConversion(_dtoConverter);
            b.Property(x => x.Phone).HasMaxLength(32);
            b.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
            // Filtered unique on (TenantId, Phone) WHERE Phone IS NOT NULL — supported by SQL Server,
            // PostgreSQL, and SQLite. Without the filter, every NULL phone would collide on the unique
            // constraint. The unquoted "Phone" name is accepted by all three providers.
            b.HasIndex(x => new { x.TenantId, x.Phone })
                .IsUnique()
                .HasFilter("Phone IS NOT NULL");
        });

        modelBuilder.Entity<MagicLinkTokenEntity>(b =>
        {
            b.ToTable("TapInAuthMagicLinkTokens");
            b.HasKey(x => x.Id);
            b.Property(x => x.TenantId).IsRequired().HasMaxLength(128);
            b.Property(x => x.Email).IsRequired().HasMaxLength(256);
            b.Property(x => x.TokenHash).IsRequired().HasMaxLength(64);
            b.Property(x => x.ReturnUrl).HasMaxLength(2048);
            b.Property(x => x.CreatedAt).HasConversion(_dtoConverter);
            b.Property(x => x.ExpiresAt).HasConversion(_dtoConverter);
            b.Property(x => x.ConsumedAt).HasConversion(_dtoNullableConverter);
            b.HasIndex(x => new { x.TenantId, x.UserId });
            b.HasIndex(x => x.ExpiresAt);
        });

        modelBuilder.Entity<OtpCodeEntity>(b =>
        {
            b.ToTable("TapInAuthOtpCodes");
            b.HasKey(x => x.Id);
            b.Property(x => x.TenantId).IsRequired().HasMaxLength(128);
            b.Property(x => x.Destination).IsRequired().HasMaxLength(256);
            b.Property(x => x.CodeHash).IsRequired().HasMaxLength(64);
            b.Property(x => x.CreatedAt).HasConversion(_dtoConverter);
            b.Property(x => x.ExpiresAt).HasConversion(_dtoConverter);
            b.Property(x => x.ConsumedAt).HasConversion(_dtoNullableConverter);
            b.HasIndex(x => new { x.TenantId, x.UserId, x.Channel });
            b.HasIndex(x => x.ExpiresAt);
        });

        modelBuilder.Entity<CredentialEntity>(b =>
        {
            b.ToTable("TapInAuthCredentials");
            b.HasKey(x => x.Id);
            b.Property(x => x.TenantId).IsRequired().HasMaxLength(128);
            b.Property(x => x.CredentialId).IsRequired().HasMaxLength(1024);
            b.Property(x => x.PublicKey).IsRequired().HasMaxLength(2048);
            b.Property(x => x.DeviceName).HasMaxLength(256);
            b.Property(x => x.CreatedAt).HasConversion(_dtoConverter);
            b.Property(x => x.LastUsedAt).HasConversion(_dtoNullableConverter);
            b.HasIndex(x => new { x.TenantId, x.CredentialId }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.UserId });
        });

        modelBuilder.Entity<RecoveryCodeEntity>(b =>
        {
            b.ToTable("TapInAuthRecoveryCodes");
            b.HasKey(x => x.Id);
            b.Property(x => x.TenantId).IsRequired().HasMaxLength(128);
            b.Property(x => x.CodeHash).IsRequired().HasMaxLength(64);
            b.Property(x => x.CreatedAt).HasConversion(_dtoConverter);
            b.Property(x => x.ConsumedAt).HasConversion(_dtoNullableConverter);
            b.HasIndex(x => new { x.TenantId, x.UserId });
        });

        modelBuilder.Entity<AuditEventEntity>(b =>
        {
            b.ToTable("TapInAuthAuditEvents");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.TenantId).IsRequired().HasMaxLength(128);
            b.Property(x => x.Email).HasMaxLength(256);
            b.Property(x => x.UserId).HasMaxLength(64);
            b.Property(x => x.IpAddress).HasMaxLength(64);
            b.Property(x => x.UserAgent).HasMaxLength(512);
            b.Property(x => x.Detail).HasMaxLength(1024);
            b.Property(x => x.Timestamp).HasConversion(_dtoConverter);
            b.HasIndex(x => new { x.TenantId, x.Timestamp });
            b.HasIndex(x => new { x.TenantId, x.Type });
        });

        return modelBuilder;
    }
}

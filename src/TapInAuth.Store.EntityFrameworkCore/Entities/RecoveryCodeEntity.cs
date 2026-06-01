namespace TapInAuth.Store.EntityFrameworkCore.Entities;

/// <summary>EF Core entity for a one-time recovery code. <see cref="CodeHash"/> is HMAC-SHA256 — never raw.</summary>
public class RecoveryCodeEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = TenantContext.DefaultTenantId;
    public Guid UserId { get; set; }
    public byte[] CodeHash { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}

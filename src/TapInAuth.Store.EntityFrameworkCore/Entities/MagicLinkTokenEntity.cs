namespace TapInAuth.Store.EntityFrameworkCore.Entities;

/// <summary>EF Core entity for a magic-link token. <see cref="TokenHash"/> is HMAC-SHA256 — never raw.</summary>
public class MagicLinkTokenEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = TenantContext.DefaultTenantId;
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public byte[] TokenHash { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public string? ReturnUrl { get; set; }
}

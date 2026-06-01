using TapInAuth.Tokens;

namespace TapInAuth.Store.EntityFrameworkCore.Entities;

/// <summary>EF Core entity for an OTP code. <see cref="CodeHash"/> is HMAC-SHA256 — never raw.</summary>
public class OtpCodeEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = TenantContext.DefaultTenantId;
    public Guid UserId { get; set; }
    public string Destination { get; set; } = string.Empty;
    public OtpChannel Channel { get; set; }
    public byte[] CodeHash { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}

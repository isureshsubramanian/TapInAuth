using TapInAuth.Auditing;

namespace TapInAuth.Store.EntityFrameworkCore.Entities;

/// <summary>EF Core entity backing the persistent <see cref="IAuditSink"/> + <see cref="IAuditQuery"/>.</summary>
public class AuditEventEntity
{
    public long Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string TenantId { get; set; } = TenantContext.DefaultTenantId;
    public AuditEventType Type { get; set; }
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Detail { get; set; }
    public bool Success { get; set; }
}

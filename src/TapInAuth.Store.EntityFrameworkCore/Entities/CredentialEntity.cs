namespace TapInAuth.Store.EntityFrameworkCore.Entities;

/// <summary>EF Core entity for a WebAuthn passkey credential (used from 0.3; schema exists from 0.1).</summary>
public class CredentialEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = TenantContext.DefaultTenantId;
    public Guid UserId { get; set; }
    public byte[] CredentialId { get; set; } = [];
    public byte[] PublicKey { get; set; } = [];
    public long SignatureCounter { get; set; }
    public Guid? Aaguid { get; set; }
    public string? DeviceName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
}

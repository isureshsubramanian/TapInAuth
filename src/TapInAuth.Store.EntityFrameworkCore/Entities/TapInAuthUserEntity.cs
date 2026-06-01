namespace TapInAuth.Store.EntityFrameworkCore.Entities;

/// <summary>EF Core entity for a TapInAuth user. Mapped to a separate table from the host's user table.</summary>
public class TapInAuthUserEntity
{
    /// <summary>Stable user ID.</summary>
    public Guid Id { get; set; }

    /// <summary>Tenant ID. Filtered unique index on (TenantId, Email).</summary>
    public string TenantId { get; set; } = TenantContext.DefaultTenantId;

    /// <summary>Email (lowercased on save).</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Whether the email has been verified by a successful magic-link or OTP redemption.</summary>
    public bool EmailVerified { get; set; }

    /// <summary>Optional display name.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Optional E.164 phone (secondary identifier). Filtered unique index on (TenantId, Phone) WHERE Phone IS NOT NULL.</summary>
    public string? Phone { get; set; }

    /// <summary>Whether the phone has been verified via SMS-OTP.</summary>
    public bool PhoneVerified { get; set; }
}

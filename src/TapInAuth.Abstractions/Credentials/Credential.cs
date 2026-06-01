namespace TapInAuth.Credentials;

/// <summary>
/// A registered passkey credential (WebAuthn / FIDO2). Used from 0.3 onward.
/// Defined now so the store schema includes the table from day one.
/// </summary>
public sealed class Credential
{
    /// <summary>Stable identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>The tenant this credential belongs to.</summary>
    public required string TenantId { get; init; }

    /// <summary>The user this credential belongs to.</summary>
    public required Guid UserId { get; init; }

    /// <summary>The WebAuthn credential ID issued by the authenticator (raw bytes). Unique within a tenant.</summary>
    public required byte[] CredentialId { get; init; }

    /// <summary>The COSE-encoded public key.</summary>
    public required byte[] PublicKey { get; init; }

    /// <summary>The signature counter from the last assertion (used to detect cloned authenticators).</summary>
    public uint SignatureCounter { get; set; }

    /// <summary>The Authenticator Attestation GUID (AAGUID), if known.</summary>
    public Guid? Aaguid { get; init; }

    /// <summary>A user-friendly device label (e.g., "iPhone 15", "YubiKey 5"). May be edited from the account page.</summary>
    public string? DeviceName { get; set; }

    /// <summary>UTC time of registration.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>UTC time of the last successful assertion (null until used).</summary>
    public DateTimeOffset? LastUsedAt { get; set; }
}

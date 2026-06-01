namespace TapInAuth.Tokens;

/// <summary>
/// A persisted recovery code. The plaintext code is shown to the user once at generation;
/// only the HMAC-SHA256 hash is stored. Single-use.
/// </summary>
public sealed class RecoveryCode
{
    /// <summary>Stable identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>The tenant this code belongs to.</summary>
    public required string TenantId { get; init; }

    /// <summary>The user this code authenticates.</summary>
    public required Guid UserId { get; init; }

    /// <summary>HMAC-SHA256 of the recovery code's plaintext. Never stored raw.</summary>
    public required byte[] CodeHash { get; init; }

    /// <summary>UTC time the code was issued.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>UTC time the code was successfully redeemed (null until used). Single-use.</summary>
    public DateTimeOffset? ConsumedAt { get; set; }
}

namespace TapInAuth.Tokens;

/// <summary>
/// A persisted one-time-passcode (OTP). Like <see cref="MagicLinkToken"/>, the code is stored only
/// as an HMAC-SHA256 hash and verified with constant-time comparison.
/// </summary>
public sealed class OtpCode
{
    /// <summary>Stable identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>The tenant this OTP belongs to.</summary>
    public required string TenantId { get; init; }

    /// <summary>The user this OTP authenticates.</summary>
    public required Guid UserId { get; init; }

    /// <summary>The destination address (email for email-OTP, phone for SMS-OTP).</summary>
    public required string Destination { get; init; }

    /// <summary>The channel used to deliver the OTP.</summary>
    public required OtpChannel Channel { get; init; }

    /// <summary>HMAC-SHA256 of the OTP code. Never stored raw.</summary>
    public required byte[] CodeHash { get; init; }

    /// <summary>UTC time the OTP was issued.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>UTC time the OTP expires.</summary>
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Number of failed verification attempts so far.</summary>
    public int AttemptCount { get; set; }

    /// <summary>UTC time the OTP was successfully verified (null until verified). Single-use.</summary>
    public DateTimeOffset? ConsumedAt { get; set; }
}

/// <summary>Delivery channel for a one-time passcode.</summary>
public enum OtpChannel
{
    /// <summary>Delivered to the user's email.</summary>
    Email,
    /// <summary>Delivered via SMS.</summary>
    Sms,
}

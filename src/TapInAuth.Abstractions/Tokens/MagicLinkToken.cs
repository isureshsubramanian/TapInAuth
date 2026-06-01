namespace TapInAuth.Tokens;

/// <summary>
/// A persisted magic-link token. The token value the user clicks in their email is NEVER stored;
/// only its HMAC-SHA256 hash. Constant-time comparison is used during redemption.
/// </summary>
public sealed class MagicLinkToken
{
    /// <summary>Stable identifier; also exposed in the magic-link URL as the lookup key.</summary>
    public Guid Id { get; init; }

    /// <summary>The tenant this token belongs to.</summary>
    public required string TenantId { get; init; }

    /// <summary>The user this token authenticates.</summary>
    public required Guid UserId { get; init; }

    /// <summary>The email this token was sent to (for audit and replay protection).</summary>
    public required string Email { get; init; }

    /// <summary>HMAC-SHA256 of the random token bytes. Never stored raw.</summary>
    public required byte[] TokenHash { get; init; }

    /// <summary>UTC time the token was issued.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>UTC time the token expires.</summary>
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>UTC time the token was successfully redeemed (null until redeemed). Single-use.</summary>
    public DateTimeOffset? ConsumedAt { get; set; }

    /// <summary>Optional return URL the user should be redirected to after successful sign-in.</summary>
    public string? ReturnUrl { get; init; }
}

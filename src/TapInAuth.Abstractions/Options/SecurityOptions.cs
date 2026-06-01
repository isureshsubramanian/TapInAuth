namespace TapInAuth.Options;

/// <summary>Security knobs: token lifetimes, rate limits, lockout policies.</summary>
public sealed class SecurityOptions
{
    /// <summary>How long a magic-link token is valid for after issuance.</summary>
    public TimeSpan MagicLinkLifetime { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>How long an OTP is valid for after issuance.</summary>
    public TimeSpan OtpLifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Length of OTP codes generated (6 or 8).</summary>
    public int OtpDigits { get; set; } = 6;

    /// <summary>Maximum failed OTP verifications before the code is invalidated.</summary>
    public int MaxOtpAttempts { get; set; } = 5;

    /// <summary>
    /// HMAC pepper (base64-encoded). Must be at least 32 bytes once decoded.
    /// Generated automatically on first run and persisted by the host; rotate via a configuration update.
    /// </summary>
    public string? TokenPepper { get; set; }

    /// <summary>Maximum sign-ins to attempt per identifier per <see cref="RateLimitWindow"/>.</summary>
    public int MaxSignInsPerWindow { get; set; } = 10;

    /// <summary>Maximum magic-link issuances per identifier per <see cref="RateLimitWindow"/>.</summary>
    public int MaxMagicLinkIssuancesPerWindow { get; set; } = 5;

    /// <summary>Maximum OTP issuances per identifier per <see cref="RateLimitWindow"/>.</summary>
    public int MaxOtpIssuancesPerWindow { get; set; } = 5;

    /// <summary>Time window over which rate limits are enforced.</summary>
    public TimeSpan RateLimitWindow { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Allow self-service account creation when an unknown email signs in. If false, sign-in fails for unknown emails.</summary>
    public bool AllowSignUp { get; set; } = true;

    /// <summary>How often the background purge job runs to delete expired tokens. Set to <see cref="TimeSpan.Zero"/> to disable.</summary>
    public TimeSpan PurgeInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Number of recovery codes generated per batch. Codes are shown once at generation time; regenerate to roll the set.</summary>
    public int RecoveryCodeCount { get; set; } = 10;

    /// <summary>Length in characters of each recovery code (excluding the hyphen). 10 = 5+5 with hyphen — easy to type and read aloud.</summary>
    public int RecoveryCodeLength { get; set; } = 10;

    /// <summary>The role required to access the built-in admin pages under <c>/auth/admin/*</c>.</summary>
    public string AdminRole { get; set; } = "TapInAuthAdmin";
}

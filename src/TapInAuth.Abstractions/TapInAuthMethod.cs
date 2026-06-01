namespace TapInAuth;

/// <summary>
/// Flags for the passwordless methods enabled in a TapInAuth installation.
/// Combine with bitwise OR (e.g., <c>TapInAuthMethod.MagicLink | TapInAuthMethod.EmailOtp</c>).
/// </summary>
[Flags]
public enum TapInAuthMethod
{
    /// <summary>No methods enabled. Authentication will fail; useful only for testing.</summary>
    None = 0,

    /// <summary>WebAuthn / FIDO2 passkeys (available from 0.3).</summary>
    Passkey = 1 << 0,

    /// <summary>Email magic link.</summary>
    MagicLink = 1 << 1,

    /// <summary>One-time code delivered via email.</summary>
    EmailOtp = 1 << 2,

    /// <summary>One-time code delivered via SMS (available from 0.3).</summary>
    SmsOtp = 1 << 3,

    /// <summary>One-time recovery code (rescue path when the user has lost their passkey / device).</summary>
    RecoveryCode = 1 << 4,

    /// <summary>All methods enabled.</summary>
    All = Passkey | MagicLink | EmailOtp | SmsOtp | RecoveryCode,
}

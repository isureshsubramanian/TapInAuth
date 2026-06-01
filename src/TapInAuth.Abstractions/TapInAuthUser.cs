namespace TapInAuth;

/// <summary>
/// The minimal user representation that TapInAuth needs to authenticate someone.
/// Hosts may store additional fields in their own user table; TapInAuth only requires these.
/// </summary>
/// <param name="Id">The stable user identifier within a tenant.</param>
/// <param name="TenantId">The tenant this user belongs to. <see cref="TenantContext.DefaultTenantId"/> for single-tenant apps.</param>
/// <param name="Email">The user's primary email (verified or pending verification).</param>
/// <param name="EmailVerified">Whether the email has been verified by a successful magic link / OTP redemption.</param>
/// <param name="CreatedAt">When the user was first created (UTC).</param>
/// <param name="DisplayName">Optional display name shown in the UI.</param>
/// <param name="Phone">
/// Optional secondary identifier — E.164 phone number (e.g. <c>+14155550100</c>). When set, the user can
/// sign in by SMS-OTP using this phone in addition to email-based methods. Set via <c>ITapInAuthUserStore.SetPhoneAsync</c>.
/// </param>
/// <param name="PhoneVerified">Whether the phone has been verified by a successful SMS-OTP redemption.</param>
public sealed record TapInAuthUser(
    Guid Id,
    string TenantId,
    string Email,
    bool EmailVerified,
    DateTimeOffset CreatedAt,
    string? DisplayName = null,
    string? Phone = null,
    bool PhoneVerified = false);

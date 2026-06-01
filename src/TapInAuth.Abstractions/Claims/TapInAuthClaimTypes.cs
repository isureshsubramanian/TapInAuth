namespace TapInAuth.Claims;

/// <summary>
/// Claim types emitted by TapInAuth on the <see cref="System.Security.Claims.ClaimsPrincipal"/>
/// it produces after successful authentication.
/// </summary>
public static class TapInAuthClaimTypes
{
    /// <summary>The tenant the user authenticated against.</summary>
    public const string Tenant = "tapinauth:tenant";

    /// <summary>The authentication method used (<c>"passkey"</c>, <c>"magiclink"</c>, <c>"emailotp"</c>, <c>"smsotp"</c>).</summary>
    public const string Amr = "tapinauth:amr";

    /// <summary>The UTC unix timestamp of authentication.</summary>
    public const string AuthTime = "tapinauth:auth_time";

    /// <summary>Whether the email is verified.</summary>
    public const string EmailVerified = "tapinauth:email_verified";

    /// <summary>The user's E.164 phone number, when set. Emitted only when the user has a phone on file.</summary>
    public const string PhoneNumber = "tapinauth:phone_number";

    /// <summary>Whether the phone is verified.</summary>
    public const string PhoneVerified = "tapinauth:phone_verified";
}

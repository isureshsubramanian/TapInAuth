using System.Globalization;
using System.Security.Claims;
using TapInAuth.Claims;

namespace TapInAuth.Core.Services;

/// <summary>Builds <see cref="ClaimsPrincipal"/> instances for users authenticated by TapInAuth.</summary>
public sealed class TapInAuthClaimsPrincipalFactory
{
    /// <summary>The authentication scheme/type stamped onto the principal's <see cref="ClaimsIdentity"/>.</summary>
    public const string AuthenticationType = "TapInAuth";

    /// <summary>Create a principal for the given user, tenant, and method.</summary>
    public ClaimsPrincipal Create(TapInAuthUser user, TenantContext tenant, TapInAuthMethod method, DateTimeOffset authTime)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(tenant);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString("D", CultureInfo.InvariantCulture)),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName ?? user.Email),
            new(TapInAuthClaimTypes.Tenant, tenant.Id),
            new(TapInAuthClaimTypes.Amr, MethodToAmr(method)),
            new(TapInAuthClaimTypes.AuthTime, authTime.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            new(TapInAuthClaimTypes.EmailVerified, user.EmailVerified ? "true" : "false"),
        };
        if (!string.IsNullOrEmpty(user.Phone))
        {
            // ClaimTypes.MobilePhone is the standard SOAP claim URI and downstream MVC binders look for it;
            // TapInAuthClaimTypes.PhoneNumber gives us a TapInAuth-namespaced alias that won't collide if the
            // host's existing principal already has a MobilePhone claim from a different source.
            claims.Add(new(ClaimTypes.MobilePhone, user.Phone));
            claims.Add(new(TapInAuthClaimTypes.PhoneNumber, user.Phone));
            claims.Add(new(TapInAuthClaimTypes.PhoneVerified, user.PhoneVerified ? "true" : "false"));
        }
        var identity = new ClaimsIdentity(claims, AuthenticationType, ClaimTypes.Name, ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }

    private static string MethodToAmr(TapInAuthMethod method) => method switch
    {
        TapInAuthMethod.Passkey   => "passkey",
        TapInAuthMethod.MagicLink => "magiclink",
        TapInAuthMethod.EmailOtp  => "emailotp",
        TapInAuthMethod.SmsOtp    => "smsotp",
        _ => "unknown",
    };
}

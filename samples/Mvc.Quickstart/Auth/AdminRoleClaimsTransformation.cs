using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using TapInAuth.Options;

namespace TapInAuth.Samples.Mvc.Auth;

/// <summary>
/// Demo-only: stamps the TapInAuth admin role onto the principal when the signed-in email matches
/// any of the addresses listed in <see cref="SampleAuthOptions.AdminEmails"/> from appsettings.
/// In a real app, role membership would come from your user/Identity store.
/// </summary>
public sealed class AdminRoleClaimsTransformation : IClaimsTransformation
{
    private readonly IOptions<SampleAuthOptions> _sample;
    private readonly IOptions<TapInAuthOptions> _tapInAuth;

    public AdminRoleClaimsTransformation(IOptions<SampleAuthOptions> sample, IOptions<TapInAuthOptions> tapInAuth)
    {
        _sample = sample;
        _tapInAuth = tapInAuth;
    }

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return Task.FromResult(principal);
        }

        var role = _tapInAuth.Value.Security.AdminRole;
        if (identity.HasClaim(ClaimTypes.Role, role))
        {
            return Task.FromResult(principal);
        }

        var email = principal.FindFirst(ClaimTypes.Email)?.Value ?? principal.Identity.Name;
        if (!string.IsNullOrWhiteSpace(email) &&
            _sample.Value.AdminEmails.Any(e => string.Equals(e, email, StringComparison.OrdinalIgnoreCase)))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }
        return Task.FromResult(principal);
    }
}

/// <summary>Sample-only options: which emails get the admin role.</summary>
public sealed class SampleAuthOptions
{
    public const string SectionName = "Sample:Auth";
    public List<string> AdminEmails { get; set; } = ["admin@example.com"];
}

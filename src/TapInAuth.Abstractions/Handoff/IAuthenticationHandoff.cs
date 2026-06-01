using System.Security.Claims;

namespace TapInAuth.Handoff;

/// <summary>
/// Hands off a verified principal to the host's authentication system.
/// TapInAuth never owns the session cookie — this contract is how a successful sign-in
/// becomes a signed-in session in the host application.
/// </summary>
/// <remarks>
/// The default implementation (<c>CookieAuthenticationHandoff</c> in <c>TapInAuth.AspNetCore</c>)
/// calls <c>HttpContext.SignInAsync</c> against the host's default authentication scheme.
/// Custom implementations can mint JWTs, call into IdentityServer, etc.
/// </remarks>
public interface IAuthenticationHandoff
{
    /// <summary>
    /// Sign the principal into the host's authentication system.
    /// </summary>
    /// <param name="context">The handoff context (HTTP request scope).</param>
    /// <param name="principal">The verified <see cref="ClaimsPrincipal"/> produced by TapInAuth.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SignInAsync(AuthenticationHandoffContext context, ClaimsPrincipal principal, CancellationToken cancellationToken = default);

    /// <summary>Sign the user out of the host's authentication system.</summary>
    Task SignOutAsync(AuthenticationHandoffContext context, CancellationToken cancellationToken = default);
}

/// <summary>Per-request context for an authentication handoff.</summary>
/// <param name="HttpContext">
/// The current <c>Microsoft.AspNetCore.Http.HttpContext</c>, boxed as <see cref="object"/> here so
/// <c>TapInAuth.Abstractions</c> stays framework-agnostic. Consumers cast to <c>HttpContext</c>.
/// </param>
/// <param name="Tenant">The tenant context for this sign-in.</param>
/// <param name="Method">The TapInAuth method used to authenticate.</param>
/// <param name="IsPersistent">Whether the host should issue a persistent (remember-me) cookie.</param>
/// <param name="ReturnUrl">Optional URL to redirect to after sign-in.</param>
public sealed record AuthenticationHandoffContext(
    object HttpContext,
    TenantContext Tenant,
    TapInAuthMethod Method,
    bool IsPersistent = false,
    string? ReturnUrl = null);

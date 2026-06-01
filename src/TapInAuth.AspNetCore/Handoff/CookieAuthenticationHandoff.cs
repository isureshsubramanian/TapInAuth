using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TapInAuth.Handoff;

namespace TapInAuth.AspNetCore.Handoff;

/// <summary>
/// Default <see cref="IAuthenticationHandoff"/>: signs the principal into the host's default
/// authentication scheme via <see cref="AuthenticationHttpContextExtensions.SignInAsync(HttpContext, System.Security.Claims.ClaimsPrincipal, AuthenticationProperties)"/>.
/// </summary>
public sealed class CookieAuthenticationHandoff : IAuthenticationHandoff
{
    private readonly ILogger<CookieAuthenticationHandoff> _logger;

    /// <summary>Construct the cookie handoff.</summary>
    public CookieAuthenticationHandoff(ILogger<CookieAuthenticationHandoff> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task SignInAsync(AuthenticationHandoffContext context, System.Security.Claims.ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(principal);

        if (context.HttpContext is not HttpContext http)
        {
            throw new InvalidOperationException("AuthenticationHandoffContext.HttpContext must be an ASP.NET Core HttpContext.");
        }

        var props = new AuthenticationProperties
        {
            IsPersistent = context.IsPersistent,
            IssuedUtc = DateTimeOffset.UtcNow,
            RedirectUri = context.ReturnUrl,
        };

        // Use the host's default sign-in scheme if it is configured; fall back to the cookie default.
        var scheme = CookieAuthenticationDefaults.AuthenticationScheme;
        _logger.LogDebug("TapInAuth cookie handoff: signing in user {Subject} into scheme {Scheme}", principal.Identity?.Name, scheme);
        await http.SignInAsync(scheme, principal, props).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SignOutAsync(AuthenticationHandoffContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.HttpContext is not HttpContext http)
        {
            throw new InvalidOperationException("AuthenticationHandoffContext.HttpContext must be an ASP.NET Core HttpContext.");
        }
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
    }
}

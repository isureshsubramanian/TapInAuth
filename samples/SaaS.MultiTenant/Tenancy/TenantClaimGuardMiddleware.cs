using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using TapInAuth.Claims;
using TapInAuth.Tenancy;

namespace TapInAuth.Samples.SaaS.Tenancy;

/// <summary>
/// For authenticated requests, compares the principal's <see cref="TapInAuthClaimTypes.Tenant"/>
/// claim (the tenant the user actually signed into) against the tenant the resolver returned for
/// the current request (the tenant the URL is pretending to be). A mismatch indicates a stale
/// cookie carried into another tenant — most often a benign user navigating away from "their"
/// tenant URL, but the cheapest fix is to sign them out and re-prompt sign-in for the new tenant.
/// </summary>
/// <remarks>
/// Place this AFTER <c>UseAuthentication()</c>/<c>UseAuthorization()</c> so the principal is
/// populated, and BEFORE <c>UseEndpoints</c>/<c>MapRazorPages</c> so the redirect happens before
/// the page renders any per-tenant data.
/// </remarks>
public static class TenantClaimGuardMiddleware
{
    public static IApplicationBuilder UseTenantClaimGuard(this IApplicationBuilder app)
    {
        return app.Use(async (ctx, next) =>
        {
            // Skip TapInAuth's own endpoints — sign-in / sign-out / passkey ceremonies don't need this guard
            // (and the sign-out endpoint would itself trip it). Also skip static assets.
            var path = ctx.Request.Path.Value ?? "";
            if (path.StartsWith("/auth/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/_content/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/hermex", StringComparison.OrdinalIgnoreCase))
            {
                await next().ConfigureAwait(false);
                return;
            }

            if (ctx.User.Identity?.IsAuthenticated == true)
            {
                var claimedTenant = ctx.User.FindFirst(TapInAuthClaimTypes.Tenant)?.Value;
                if (!string.IsNullOrEmpty(claimedTenant))
                {
                    var resolver = ctx.RequestServices.GetRequiredService<ITenantResolver>();
                    var resolved = (await resolver.ResolveAsync(ctx.RequestAborted).ConfigureAwait(false))
                                   ?? TenantContext.Default;

                    // Only treat as a mismatch when the URL explicitly points at a DIFFERENT non-default
                    // tenant. A "default" resolution means the request has no tenant signal (e.g., the
                    // post-sign-in redirect to "/" without the dev ?tenant= query), in which case we
                    // trust the cookie. A non-default resolution that differs from the cookie means
                    // someone navigated into another tenant's branded URL — sign them out.
                    if (resolved.Id != TenantContext.DefaultTenantId &&
                        !string.Equals(resolved.Id, claimedTenant, StringComparison.Ordinal))
                    {
                        await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
                        ctx.Response.Redirect("/auth/sign-in?error=tenant_mismatch");
                        return;
                    }
                }
            }
            await next().ConfigureAwait(false);
        });
    }
}

using TapInAuth.AspNetCore.Tenancy;

namespace TapInAuth.Samples.SaaS.Tenancy;

/// <summary>
/// Resolves tenant from the first DNS label (e.g., <c>acme.localhost</c> → tenant <c>acme</c>).
/// In Development only, also honors a <c>?tenant=</c> query-string override so you can swap tenants
/// without editing your hosts file. The override is intentionally disabled outside Development so
/// authenticated users can't spoof their way into another tenant's branding in production.
/// </summary>
public sealed class CatalogTenantResolver(
    IHttpContextAccessor accessor,
    InMemoryTenantCatalog catalog,
    IHostEnvironment environment) : HttpTenantResolver(accessor)
{
    private readonly InMemoryTenantCatalog _catalog = catalog;
    private readonly IHostEnvironment _environment = environment;

    /// <inheritdoc />
    protected override ValueTask<TenantContext?> ResolveFromHttpContextAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        // 1. Query-string override — DEV ONLY. Honored only when ASPNETCORE_ENVIRONMENT=Development.
        if (_environment.IsDevelopment())
        {
            // 1a. Direct ?tenant=acme on this request.
            var fromQuery = httpContext.Request.Query["tenant"].ToString();
            if (!string.IsNullOrWhiteSpace(fromQuery))
            {
                return ValueTask.FromResult(_catalog.TryGet(fromQuery));
            }

            // 1b. Cookie-auth's LoginPath bounce wraps the original URL in ?ReturnUrl=...; peek inside
            //     so the tenant context survives the redirect to /auth/sign-in. Otherwise the user signs
            //     in as the default tenant and then trips the tenant-claim guard on the way back.
            var returnUrl = httpContext.Request.Query["ReturnUrl"].ToString();
            if (!string.IsNullOrWhiteSpace(returnUrl) &&
                Uri.TryCreate(new Uri(httpContext.Request.Scheme + "://" + httpContext.Request.Host), returnUrl, out var ru) &&
                !string.IsNullOrEmpty(ru.Query))
            {
                var fromReturnUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(ru.Query);
                if (fromReturnUrl.TryGetValue("tenant", out var t) && !string.IsNullOrWhiteSpace(t))
                {
                    var match = _catalog.TryGet(t.ToString());
                    if (match is not null)
                    {
                        return ValueTask.FromResult<TenantContext?>(match);
                    }
                }
            }
        }

        // 2. Subdomain (acme.localhost, globex.localhost, etc.) — the production path.
        var host = httpContext.Request.Host.Host;
        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            var slug = parts[0];
            var match = _catalog.TryGet(slug);
            if (match is not null)
            {
                return ValueTask.FromResult<TenantContext?>(match);
            }
        }

        // 3. Fallback.
        return ValueTask.FromResult<TenantContext?>(TenantContext.Default);
    }
}

using Microsoft.AspNetCore.Http;
using TapInAuth.Tenancy;

namespace TapInAuth.AspNetCore.Tenancy;

/// <summary>
/// Convenience base class for tenant resolvers that need <see cref="HttpContext"/>.
/// Derived classes implement <see cref="ResolveFromHttpContextAsync"/>.
/// </summary>
public abstract class HttpTenantResolver : ITenantResolver
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>Constructor.</summary>
    protected HttpTenantResolver(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <inheritdoc />
    public ValueTask<TenantContext?> ResolveAsync(CancellationToken cancellationToken = default)
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is null)
        {
            return ValueTask.FromResult<TenantContext?>(null);
        }
        return ResolveFromHttpContextAsync(ctx, cancellationToken);
    }

    /// <summary>Resolve the tenant from an active <see cref="HttpContext"/>.</summary>
    protected abstract ValueTask<TenantContext?> ResolveFromHttpContextAsync(HttpContext httpContext, CancellationToken cancellationToken);
}

/// <summary>
/// Resolves the tenant from the first subdomain of the request host.
/// e.g., for <c>acme.example.com</c> the tenant id is <c>"acme"</c>.
/// </summary>
public sealed class SubdomainTenantResolver : HttpTenantResolver
{
    /// <summary>Constructor.</summary>
    public SubdomainTenantResolver(IHttpContextAccessor httpContextAccessor) : base(httpContextAccessor) { }

    /// <inheritdoc />
    protected override ValueTask<TenantContext?> ResolveFromHttpContextAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        var host = httpContext.Request.Host.Host;
        if (string.IsNullOrEmpty(host))
        {
            return ValueTask.FromResult<TenantContext?>(null);
        }
        // localhost / IP addresses: fall back to default
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || System.Net.IPAddress.TryParse(host, out _))
        {
            return ValueTask.FromResult<TenantContext?>(TenantContext.Default);
        }
        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return ValueTask.FromResult<TenantContext?>(TenantContext.Default);
        }
        var slug = parts[0].ToLowerInvariant();
        if (slug is "www" or "app")
        {
            return ValueTask.FromResult<TenantContext?>(TenantContext.Default);
        }
        return ValueTask.FromResult<TenantContext?>(new TenantContext(slug, slug));
    }
}

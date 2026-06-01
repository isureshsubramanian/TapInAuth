namespace TapInAuth.Tenancy;

/// <summary>
/// Resolves the current tenant for an inbound request. Optional — single-tenant apps don't register one.
/// </summary>
/// <remarks>
/// The library ships <c>NullTenantResolver</c> (always returns <see cref="TenantContext.Default"/>) as the fallback.
/// Subdomain, route-segment, host-header, and claims-based resolvers ship as ready-to-use implementations
/// in <c>TapInAuth.AspNetCore</c>.
/// </remarks>
public interface ITenantResolver
{
    /// <summary>
    /// Resolve the tenant for the current operation. Implementations may consult the current
    /// ASP.NET Core <c>HttpContext</c> via a context accessor, or use any other ambient state.
    /// </summary>
    /// <returns>The tenant context, or null if no tenant could be resolved.</returns>
    ValueTask<TenantContext?> ResolveAsync(CancellationToken cancellationToken = default);
}

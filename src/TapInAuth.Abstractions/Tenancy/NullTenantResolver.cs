namespace TapInAuth.Tenancy;

/// <summary>
/// Default <see cref="ITenantResolver"/> that always returns <see cref="TenantContext.Default"/>.
/// Single-tenant applications use this implicitly when no other resolver is registered.
/// </summary>
public sealed class NullTenantResolver : ITenantResolver
{
    /// <inheritdoc />
    public ValueTask<TenantContext?> ResolveAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult<TenantContext?>(TenantContext.Default);
}

namespace TapInAuth;

/// <summary>
/// Context describing the tenant for which a TapInAuth operation is being performed.
/// In single-tenant apps, <see cref="Id"/> is the well-known string <c>"default"</c> and
/// most fields are <c>null</c>; consumers can ignore tenancy entirely.
/// </summary>
/// <param name="Id">Stable tenant identifier (slug). Lowercased, no whitespace.</param>
/// <param name="DisplayName">Optional human-readable tenant name shown in the UI.</param>
/// <param name="RelyingPartyId">
/// Optional WebAuthn relying-party ID for this tenant (e.g., <c>"acme.example.com"</c>).
/// If null, the global RP ID is used.
/// </param>
/// <param name="LogoPath">Optional per-tenant logo path; overrides the global logo.</param>
/// <param name="ThemeAccent">Optional per-tenant accent color override.</param>
public sealed record TenantContext(
    string Id,
    string? DisplayName = null,
    string? RelyingPartyId = null,
    string? LogoPath = null,
    string? ThemeAccent = null)
{
    /// <summary>The default tenant used by single-tenant installations.</summary>
    public const string DefaultTenantId = "default";

    /// <summary>The default tenant instance.</summary>
    public static TenantContext Default { get; } = new(DefaultTenantId);
}

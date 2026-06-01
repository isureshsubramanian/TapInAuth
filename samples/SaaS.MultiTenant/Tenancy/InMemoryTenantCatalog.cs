namespace TapInAuth.Samples.SaaS.Tenancy;

/// <summary>
/// Demo tenant catalog. In a real SaaS this comes from a database, the host header,
/// a configuration system, or the customer signup flow. Sample hardcodes three.
/// </summary>
public sealed class InMemoryTenantCatalog
{
    private static readonly Dictionary<string, TenantContext> _tenants = new(StringComparer.OrdinalIgnoreCase)
    {
        ["acme"]    = new("acme",    "Acme, Inc.",    LogoPath: "img/acme-logo.svg",  ThemeAccent: "#2563EB"),
        ["globex"]  = new("globex",  "Globex Corp",   LogoPath: "img/globex-logo.svg", ThemeAccent: "#059669"),
        ["initech"] = new("initech", "Initech",       LogoPath: "img/initech-logo.svg", ThemeAccent: "#DC2626"),
    };

    public TenantContext? TryGet(string slug)
        => _tenants.TryGetValue(slug, out var tenant) ? tenant : null;
}

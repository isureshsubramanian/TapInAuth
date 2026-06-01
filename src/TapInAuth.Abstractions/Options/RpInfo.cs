namespace TapInAuth.Options;

/// <summary>WebAuthn relying-party info. Used from 0.3 for passkey support.</summary>
public sealed class RpInfo
{
    /// <summary>
    /// The relying-party ID. Must be the apex domain or a registrable suffix of the origin
    /// (e.g., <c>"example.com"</c> for both <c>"www.example.com"</c> and <c>"app.example.com"</c>).
    /// </summary>
    public string? Id { get; set; }

    /// <summary>The relying-party display name shown to the user during passkey ceremonies.</summary>
    public string Name { get; set; } = "TapInAuth";

    /// <summary>The set of allowed origins for WebAuthn ceremonies. At least one must be configured.</summary>
    public IList<string> AllowedOrigins { get; } = new List<string>();
}

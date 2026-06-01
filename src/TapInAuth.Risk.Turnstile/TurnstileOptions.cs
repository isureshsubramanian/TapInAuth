using System.ComponentModel.DataAnnotations;

namespace TapInAuth.Risk.Turnstile;

/// <summary>Cloudflare Turnstile configuration.</summary>
public sealed class TurnstileOptions
{
    /// <summary>Default options section name for binding (<c>"TapInAuth:Turnstile"</c>).</summary>
    public const string SectionName = "TapInAuth:Turnstile";

    /// <summary>Public site key from the Cloudflare dashboard.</summary>
    [Required]
    public string SiteKey { get; set; } = string.Empty;

    /// <summary>Secret key for server-side verification. Keep in a secret store.</summary>
    [Required]
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>How strict to be on a non-success outcome. <c>Block</c> rejects the request; <c>Challenge</c> would prompt step-up if your host wires that.</summary>
    public RiskLevel FailureLevel { get; set; } = RiskLevel.Block;
}

using System.ComponentModel.DataAnnotations;

namespace TapInAuth.Risk.HCaptcha;

/// <summary>hCaptcha configuration.</summary>
public sealed class HCaptchaOptions
{
    /// <summary>Default options section name for binding (<c>"TapInAuth:HCaptcha"</c>).</summary>
    public const string SectionName = "TapInAuth:HCaptcha";

    /// <summary>Public site key from the hCaptcha dashboard.</summary>
    [Required]
    public string SiteKey { get; set; } = string.Empty;

    /// <summary>Secret key for server-side verification. Keep in a secret store.</summary>
    [Required]
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>How strict to be on a non-success outcome.</summary>
    public RiskLevel FailureLevel { get; set; } = RiskLevel.Block;
}

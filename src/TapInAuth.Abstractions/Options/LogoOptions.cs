namespace TapInAuth.Options;

/// <summary>Branding logo configuration.</summary>
public sealed class LogoOptions
{
    /// <summary>Path to a single logo file (SVG preferred, PNG accepted). Used in both light and dark modes.</summary>
    public string? Path { get; set; }

    /// <summary>Optional separate dark-mode logo. If null, the library auto-handles the background plate.</summary>
    public string? DarkPath { get; set; }

    /// <summary>Maximum logo width in CSS pixels when rendered in the UI.</summary>
    public int MaxWidthPx { get; set; } = 240;

    /// <summary>Maximum logo height in CSS pixels when rendered in the UI.</summary>
    public int MaxHeightPx { get; set; } = 80;

    /// <summary>
    /// Optional alternative text for the logo. Defaults to the product/app name from configuration.
    /// </summary>
    public string? AltText { get; set; }
}

namespace TapInAuth.Options;

/// <summary>Theme tokens and dark/light mode behavior for the TapInAuth UI.</summary>
public sealed class ThemeOptions
{
    /// <summary>The primary accent color in hex (e.g., <c>"#2563EB"</c>). Required.</summary>
    public string Accent { get; set; } = "#2563EB";

    /// <summary>The CSS background variable for dark mode.</summary>
    public string BackgroundDark { get; set; } = "#0B0F19";

    /// <summary>The elevated surface color in dark mode.</summary>
    public string SurfaceDark { get; set; } = "#111827";

    /// <summary>The CSS background variable for light mode.</summary>
    public string BackgroundLight { get; set; } = "#F9FAFB";

    /// <summary>The elevated surface color in light mode.</summary>
    public string SurfaceLight { get; set; } = "#FFFFFF";

    /// <summary>The CSS border-radius for buttons and inputs.</summary>
    public string Radius { get; set; } = "14px";

    /// <summary>The CSS border-radius for cards and the auth panel.</summary>
    public string CardRadius { get; set; } = "18px";

    /// <summary>The CSS font-family. Defaults to a system-font stack.</summary>
    public string FontFamily { get; set; } = "Inter, system-ui, -apple-system, 'Segoe UI', sans-serif";

    /// <summary>The CSS monospace font family.</summary>
    public string MonoFontFamily { get; set; } = "'JetBrains Mono', ui-monospace, SFMono-Regular, monospace";

    /// <summary>Default theme mode behavior.</summary>
    public ThemeMode Mode { get; set; } = ThemeMode.Auto;
}

/// <summary>Default theme mode behavior.</summary>
public enum ThemeMode
{
    /// <summary>Follow the user agent's <c>prefers-color-scheme</c>.</summary>
    Auto,
    /// <summary>Force light mode.</summary>
    Light,
    /// <summary>Force dark mode.</summary>
    Dark,
}

using System.ComponentModel.DataAnnotations;

namespace TapInAuth.Options;

/// <summary>The root TapInAuth configuration object.</summary>
public sealed class TapInAuthOptions
{
    /// <summary>The default options section name for binding from configuration (<c>"TapInAuth"</c>).</summary>
    public const string SectionName = "TapInAuth";

    /// <summary>Which authentication methods are enabled.</summary>
    public TapInAuthMethod Methods { get; set; } = TapInAuthMethod.MagicLink | TapInAuthMethod.EmailOtp;

    /// <summary>Branding logo configuration.</summary>
    public LogoOptions Logo { get; } = new();

    /// <summary>Theme tokens and dark/light mode behavior.</summary>
    public ThemeOptions Theme { get; } = new();

    /// <summary>Endpoint route configuration (e.g., <c>/auth/sign-in</c>).</summary>
    public RoutesOptions Routes { get; } = new();

    /// <summary>Security knobs: token TTLs, rate limits, lockout.</summary>
    public SecurityOptions Security { get; } = new();

    /// <summary>WebAuthn relying-party information (used from 0.3).</summary>
    public RpInfo Relying { get; } = new();

    /// <summary>Telemetry behavior; opt-in only.</summary>
    public TelemetryOptions Telemetry { get; } = new();

    /// <summary>Default sender address used by the email provider if it does not override.</summary>
    [EmailAddress]
    public string? FromEmail { get; set; }

    /// <summary>Default sender display name.</summary>
    public string? FromDisplayName { get; set; }
}

using Microsoft.Extensions.Options;

namespace TapInAuth.Risk.Turnstile;

/// <summary><see cref="IRiskWidgetDescriptor"/> for the Cloudflare Turnstile widget.</summary>
public sealed class TurnstileWidgetDescriptor : IRiskWidgetDescriptor
{
    /// <inheritdoc />
    public string ProviderName => "turnstile";

    /// <inheritdoc />
    public string SiteKey { get; }

    /// <inheritdoc />
    public string ScriptUrl => "https://challenges.cloudflare.com/turnstile/v0/api.js";

    /// <inheritdoc />
    public string CssClass => "cf-turnstile";

    /// <inheritdoc />
    public string FormFieldName => "cf-turnstile-response";

    /// <summary>Construct the descriptor.</summary>
    public TurnstileWidgetDescriptor(IOptions<TurnstileOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        SiteKey = options.Value.SiteKey;
    }
}

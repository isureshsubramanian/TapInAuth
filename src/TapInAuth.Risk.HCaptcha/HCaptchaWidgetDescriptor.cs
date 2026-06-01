using Microsoft.Extensions.Options;

namespace TapInAuth.Risk.HCaptcha;

/// <summary><see cref="IRiskWidgetDescriptor"/> for the hCaptcha widget.</summary>
public sealed class HCaptchaWidgetDescriptor : IRiskWidgetDescriptor
{
    /// <inheritdoc />
    public string ProviderName => "hcaptcha";

    /// <inheritdoc />
    public string SiteKey { get; }

    /// <inheritdoc />
    public string ScriptUrl => "https://js.hcaptcha.com/1/api.js";

    /// <inheritdoc />
    public string CssClass => "h-captcha";

    /// <inheritdoc />
    public string FormFieldName => "h-captcha-response";

    /// <summary>Construct the descriptor.</summary>
    public HCaptchaWidgetDescriptor(IOptions<HCaptchaOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        SiteKey = options.Value.SiteKey;
    }
}

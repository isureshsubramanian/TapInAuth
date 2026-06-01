namespace TapInAuth.Risk;

/// <summary>
/// Describes the bot-defense widget the sign-in UI should render for the configured
/// <see cref="IRiskSignalProvider"/>. When no descriptor is registered the UI omits any widget
/// and the issuance endpoints skip the risk gate.
/// </summary>
public interface IRiskWidgetDescriptor
{
    /// <summary>Short provider name (e.g., <c>"turnstile"</c>, <c>"hcaptcha"</c>).</summary>
    string ProviderName { get; }

    /// <summary>The public site key the widget needs to render.</summary>
    string SiteKey { get; }

    /// <summary>URL of the provider's widget JS, to inject as a &lt;script&gt; tag.</summary>
    string ScriptUrl { get; }

    /// <summary>CSS class the widget JS uses to find its placeholder div (e.g., <c>"cf-turnstile"</c>).</summary>
    string CssClass { get; }

    /// <summary>Form field name the widget JS injects with the verification token (e.g., <c>"cf-turnstile-response"</c>).</summary>
    string FormFieldName { get; }
}

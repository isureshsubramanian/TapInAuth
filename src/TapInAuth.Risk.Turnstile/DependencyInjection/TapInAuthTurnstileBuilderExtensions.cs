using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TapInAuth.AspNetCore.DependencyInjection;


namespace TapInAuth.Risk.Turnstile.DependencyInjection;

/// <summary>Add Cloudflare Turnstile to a <see cref="TapInAuthBuilder"/>.</summary>
public static class TapInAuthTurnstileBuilderExtensions
{
    /// <summary>Register Turnstile with options bound from configuration.</summary>
    public static TapInAuthBuilder AddTurnstile(this TapInAuthBuilder builder, IConfiguration configurationSection)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configurationSection);
        builder.Services.AddOptions<TurnstileOptions>().Bind(configurationSection).ValidateDataAnnotations();
        builder.Services.AddHttpClient<TurnstileRiskSignalProvider>();
        builder.Services.AddSingleton<IRiskSignalProvider>(sp => sp.GetRequiredService<TurnstileRiskSignalProvider>());
        builder.Services.AddSingleton<IRiskWidgetDescriptor, TurnstileWidgetDescriptor>();
        return builder;
    }

    /// <summary>Register Turnstile with inline option configuration.</summary>
    public static TapInAuthBuilder AddTurnstile(this TapInAuthBuilder builder, Action<TurnstileOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        builder.Services.AddOptions<TurnstileOptions>().Configure(configure).ValidateDataAnnotations();
        builder.Services.AddHttpClient<TurnstileRiskSignalProvider>();
        builder.Services.AddSingleton<IRiskSignalProvider>(sp => sp.GetRequiredService<TurnstileRiskSignalProvider>());
        builder.Services.AddSingleton<IRiskWidgetDescriptor, TurnstileWidgetDescriptor>();
        return builder;
    }
}

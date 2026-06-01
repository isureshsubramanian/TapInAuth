using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TapInAuth.AspNetCore.DependencyInjection;


namespace TapInAuth.Risk.HCaptcha.DependencyInjection;

/// <summary>Add hCaptcha to a <see cref="TapInAuthBuilder"/>.</summary>
public static class TapInAuthHCaptchaBuilderExtensions
{
    /// <summary>Register hCaptcha with options bound from configuration.</summary>
    public static TapInAuthBuilder AddHCaptcha(this TapInAuthBuilder builder, IConfiguration configurationSection)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configurationSection);
        builder.Services.AddOptions<HCaptchaOptions>().Bind(configurationSection).ValidateDataAnnotations();
        builder.Services.AddHttpClient<HCaptchaRiskSignalProvider>();
        builder.Services.AddSingleton<IRiskSignalProvider>(sp => sp.GetRequiredService<HCaptchaRiskSignalProvider>());
        builder.Services.AddSingleton<IRiskWidgetDescriptor, HCaptchaWidgetDescriptor>();
        return builder;
    }

    /// <summary>Register hCaptcha with inline option configuration.</summary>
    public static TapInAuthBuilder AddHCaptcha(this TapInAuthBuilder builder, Action<HCaptchaOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        builder.Services.AddOptions<HCaptchaOptions>().Configure(configure).ValidateDataAnnotations();
        builder.Services.AddHttpClient<HCaptchaRiskSignalProvider>();
        builder.Services.AddSingleton<IRiskSignalProvider>(sp => sp.GetRequiredService<HCaptchaRiskSignalProvider>());
        builder.Services.AddSingleton<IRiskWidgetDescriptor, HCaptchaWidgetDescriptor>();
        return builder;
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TapInAuth.AspNetCore.DependencyInjection;
using TapInAuth.Delivery;

namespace TapInAuth.Sms.Twilio.DependencyInjection;

/// <summary>Add the Twilio SMS provider to a <see cref="TapInAuthBuilder"/>.</summary>
public static class TapInAuthTwilioBuilderExtensions
{
    /// <summary>Register the Twilio SMS sender with options bound from configuration.</summary>
    public static TapInAuthBuilder AddTwilioSms(this TapInAuthBuilder builder, IConfiguration configurationSection)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configurationSection);
        builder.Services.AddOptions<TwilioSmsOptions>().Bind(configurationSection).ValidateDataAnnotations();
        builder.Services.AddSingleton<ISmsSender, TwilioSmsSender>();
        return builder;
    }

    /// <summary>Register the Twilio SMS sender with inline option configuration.</summary>
    public static TapInAuthBuilder AddTwilioSms(this TapInAuthBuilder builder, Action<TwilioSmsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        builder.Services.AddOptions<TwilioSmsOptions>().Configure(configure).ValidateDataAnnotations();
        builder.Services.AddSingleton<ISmsSender, TwilioSmsSender>();
        return builder;
    }
}

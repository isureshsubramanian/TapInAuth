using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TapInAuth.AspNetCore.DependencyInjection;
using TapInAuth.Delivery;

namespace TapInAuth.Email.Smtp.DependencyInjection;

/// <summary>Add the SMTP email provider to a <see cref="TapInAuthBuilder"/>.</summary>
public static class TapInAuthSmtpBuilderExtensions
{
    /// <summary>Register the SMTP email sender with options bound from configuration.</summary>
    public static TapInAuthBuilder AddSmtpEmail(this TapInAuthBuilder builder, IConfiguration configurationSection)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configurationSection);
        builder.Services.AddOptions<SmtpEmailOptions>().Bind(configurationSection).ValidateDataAnnotations();
        builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
        return builder;
    }

    /// <summary>Register the SMTP email sender with inline option configuration.</summary>
    public static TapInAuthBuilder AddSmtpEmail(this TapInAuthBuilder builder, Action<SmtpEmailOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        builder.Services.AddOptions<SmtpEmailOptions>().Configure(configure).ValidateDataAnnotations();
        builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
        return builder;
    }
}

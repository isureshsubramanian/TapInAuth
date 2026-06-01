using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SendGrid;
using TapInAuth.AspNetCore.DependencyInjection;
using TapInAuth.Delivery;

namespace TapInAuth.Email.SendGrid.DependencyInjection;

/// <summary>Add the SendGrid email provider to a <see cref="TapInAuthBuilder"/>.</summary>
public static class TapInAuthSendGridBuilderExtensions
{
    /// <summary>Register the SendGrid email sender with options bound from configuration.</summary>
    public static TapInAuthBuilder AddSendGridEmail(this TapInAuthBuilder builder, IConfiguration configurationSection)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configurationSection);
        builder.Services.AddOptions<SendGridEmailOptions>().Bind(configurationSection).ValidateDataAnnotations();
        return AddCore(builder);
    }

    /// <summary>Register the SendGrid email sender with inline option configuration.</summary>
    public static TapInAuthBuilder AddSendGridEmail(this TapInAuthBuilder builder, Action<SendGridEmailOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        builder.Services.AddOptions<SendGridEmailOptions>().Configure(configure).ValidateDataAnnotations();
        return AddCore(builder);
    }

    private static TapInAuthBuilder AddCore(TapInAuthBuilder builder)
    {
        // Resolved once per send via DI — SendGridClient is thread-safe and uses a static HttpClient internally.
        builder.Services.AddSingleton<ISendGridClient>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<SendGridEmailOptions>>().Value;
            return new SendGridClient(opts.ApiKey);
        });
        builder.Services.AddSingleton<IEmailSender, SendGridEmailSender>();
        return builder;
    }
}

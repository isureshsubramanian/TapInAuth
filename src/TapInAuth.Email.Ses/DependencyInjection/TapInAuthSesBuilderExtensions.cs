using Amazon;
using Amazon.Runtime;
using Amazon.SimpleEmailV2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TapInAuth.AspNetCore.DependencyInjection;
using TapInAuth.Delivery;

namespace TapInAuth.Email.Ses.DependencyInjection;

/// <summary>Add the Amazon SES email provider to a <see cref="TapInAuthBuilder"/>.</summary>
public static class TapInAuthSesBuilderExtensions
{
    /// <summary>Register the SES email sender with options bound from configuration.</summary>
    public static TapInAuthBuilder AddSesEmail(this TapInAuthBuilder builder, IConfiguration configurationSection)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configurationSection);
        builder.Services.AddOptions<SesEmailOptions>().Bind(configurationSection).ValidateDataAnnotations();
        return AddCore(builder);
    }

    /// <summary>Register the SES email sender with inline option configuration.</summary>
    public static TapInAuthBuilder AddSesEmail(this TapInAuthBuilder builder, Action<SesEmailOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        builder.Services.AddOptions<SesEmailOptions>().Configure(configure).ValidateDataAnnotations();
        return AddCore(builder);
    }

    private static TapInAuthBuilder AddCore(TapInAuthBuilder builder)
    {
        // If the host already registered an SES client (e.g. via AWSSDK.Extensions.NETCore.Setup),
        // honor it. Otherwise build one from the options block — explicit creds only when supplied;
        // null creds let the SDK do its default credential resolution (env vars / IAM role / profile).
        builder.Services.TryAddSesClient();
        builder.Services.AddSingleton<IEmailSender, SesEmailSender>();
        return builder;
    }

    private static void TryAddSesClient(this IServiceCollection services)
    {
        services.AddSingleton<IAmazonSimpleEmailServiceV2>(sp =>
        {
            // If something else has already registered IAmazonSimpleEmailServiceV2 in this collection
            // (e.g. AddAWSService<IAmazonSimpleEmailServiceV2>()), DI will pick the last registration —
            // which lets the host override us simply by registering after AddSesEmail.
            var opts = sp.GetRequiredService<IOptions<SesEmailOptions>>().Value;
            var config = new AmazonSimpleEmailServiceV2Config();
            if (!string.IsNullOrWhiteSpace(opts.Region))
            {
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(opts.Region);
            }

            if (!string.IsNullOrWhiteSpace(opts.AccessKey) && !string.IsNullOrWhiteSpace(opts.SecretKey))
            {
                return new AmazonSimpleEmailServiceV2Client(
                    new BasicAWSCredentials(opts.AccessKey, opts.SecretKey),
                    config);
            }

            return new AmazonSimpleEmailServiceV2Client(config);
        });
    }
}

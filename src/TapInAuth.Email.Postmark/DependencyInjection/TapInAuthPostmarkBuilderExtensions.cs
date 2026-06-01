using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PostmarkDotNet;
using TapInAuth.AspNetCore.DependencyInjection;
using TapInAuth.Delivery;

namespace TapInAuth.Email.Postmark.DependencyInjection;

/// <summary>Add the Postmark email provider to a <see cref="TapInAuthBuilder"/>.</summary>
public static class TapInAuthPostmarkBuilderExtensions
{
    /// <summary>Register the Postmark email sender with options bound from configuration.</summary>
    public static TapInAuthBuilder AddPostmarkEmail(this TapInAuthBuilder builder, IConfiguration configurationSection)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configurationSection);
        builder.Services.AddOptions<PostmarkEmailOptions>().Bind(configurationSection).ValidateDataAnnotations();
        return AddCore(builder);
    }

    /// <summary>Register the Postmark email sender with inline option configuration.</summary>
    public static TapInAuthBuilder AddPostmarkEmail(this TapInAuthBuilder builder, Action<PostmarkEmailOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        builder.Services.AddOptions<PostmarkEmailOptions>().Configure(configure).ValidateDataAnnotations();
        return AddCore(builder);
    }

    private static TapInAuthBuilder AddCore(TapInAuthBuilder builder)
    {
        builder.Services.AddSingleton<PostmarkClient>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<PostmarkEmailOptions>>().Value;
            return new PostmarkClient(opts.ServerToken);
        });
        builder.Services.AddSingleton<IEmailSender, PostmarkEmailSender>();
        return builder;
    }
}

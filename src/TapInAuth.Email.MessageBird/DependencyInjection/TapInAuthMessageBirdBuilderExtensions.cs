using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TapInAuth.AspNetCore.DependencyInjection;
using TapInAuth.Delivery;

namespace TapInAuth.Email.MessageBird.DependencyInjection;

/// <summary>Add the MessageBird (Bird) email provider to a <see cref="TapInAuthBuilder"/>.</summary>
public static class TapInAuthMessageBirdBuilderExtensions
{
    /// <summary>Register the MessageBird email sender with options bound from configuration.</summary>
    public static TapInAuthBuilder AddMessageBirdEmail(this TapInAuthBuilder builder, IConfiguration configurationSection)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configurationSection);
        builder.Services.AddOptions<MessageBirdEmailOptions>().Bind(configurationSection).ValidateDataAnnotations();
        return AddCore(builder);
    }

    /// <summary>Register the MessageBird email sender with inline option configuration.</summary>
    public static TapInAuthBuilder AddMessageBirdEmail(this TapInAuthBuilder builder, Action<MessageBirdEmailOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        builder.Services.AddOptions<MessageBirdEmailOptions>().Configure(configure).ValidateDataAnnotations();
        return AddCore(builder);
    }

    private static TapInAuthBuilder AddCore(TapInAuthBuilder builder)
    {
        // Named HttpClient — lets hosts attach Polly policies via
        //   services.AddHttpClient(MessageBirdEmailSender.HttpClientName).AddStandardResilienceHandler()
        // Using a named client (resolved via IHttpClientFactory.CreateClient in the sender) keeps the
        // sender itself safe to register as a singleton without capturing a stale HttpMessageHandler.
        builder.Services.AddHttpClient(MessageBirdEmailSender.HttpClientName);
        builder.Services.AddSingleton<IEmailSender, MessageBirdEmailSender>();
        return builder;
    }
}

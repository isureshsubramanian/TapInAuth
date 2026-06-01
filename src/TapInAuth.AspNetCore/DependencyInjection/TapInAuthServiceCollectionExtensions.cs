using Fido2NetLib;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TapInAuth.AspNetCore.Handoff;
using TapInAuth.Auditing;
using TapInAuth.Core.Auditing;
using TapInAuth.Core.Delivery;
using TapInAuth.Core.RateLimiting;
using TapInAuth.Core.Security;
using TapInAuth.Core.Services;
using TapInAuth.Delivery;
using TapInAuth.Handoff;
using TapInAuth.Options;
using TapInAuth.RateLimiting;
using TapInAuth.Stores;
using TapInAuth.Tenancy;

namespace TapInAuth.AspNetCore.DependencyInjection;

/// <summary>The DI entry point for TapInAuth.</summary>
public static class TapInAuthServiceCollectionExtensions
{
    /// <summary>
    /// Register TapInAuth in the service collection.
    /// Returns a builder so additional packages (EF Core store, SMTP email, etc.) can chain their setup.
    /// </summary>
    public static TapInAuthBuilder AddTapInAuth(this IServiceCollection services, Action<TapInAuthOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<TapInAuthOptions>()
            .Configure(o =>
            {
                if (configure is not null)
                {
                    configure(o);
                }
            })
            .Validate(ValidateOptions, "TapInAuth options are invalid.");

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<TokenHasher>();
        services.TryAddSingleton<TapInAuthClaimsPrincipalFactory>();

        // Rate limiter — pulls limits from options at construction time.
        services.TryAddSingleton<IRateLimiter>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<TapInAuthOptions>>().Value;
            return new InMemoryRateLimiter(opts.Security.RateLimitWindow, Math.Max(
                opts.Security.MaxMagicLinkIssuancesPerWindow + opts.Security.MaxOtpIssuancesPerWindow,
                opts.Security.MaxSignInsPerWindow));
        });

        services.TryAddSingleton<IAuditSink, LoggingAuditSink>();
        services.TryAddSingleton<ITenantResolver, NullTenantResolver>();
        services.TryAddSingleton<IAuthenticationHandoff, CookieAuthenticationHandoff>();

        // Scoped because they depend on scoped stores (EF Core DbContext is scoped).
        services.TryAddScoped<MagicLinkService>();
        services.TryAddScoped<EmailOtpService>();
        services.TryAddScoped<RecoveryCodeService>();

        // SmsOtpService needs ISmsSender, which is contributed by a separate package
        // (TapInAuth.Sms.Twilio, etc.) that may or may not be referenced. Using a factory
        // registration here keeps the DI container's build-time validation happy even when
        // no SMS sender is present — the factory only runs when something resolves SmsOtpService.
        // If a host enables Methods.SmsOtp but forgets to add a sender, this throws a clear
        // error at the first /auth/sms/* request rather than a cryptic constructor failure.
        services.TryAddScoped<SmsOtpService>(sp => new SmsOtpService(
            sp.GetRequiredService<IOptions<TapInAuthOptions>>(),
            sp.GetRequiredService<ITapInAuthUserStore>(),
            sp.GetRequiredService<IOtpCodeStore>(),
            sp.GetService<ISmsSender>() ?? throw new InvalidOperationException(
                "TapInAuth: SmsOtp is enabled but no ISmsSender is registered. " +
                "Call .AddTwilioSms(...) (or another ISmsSender) after AddTapInAuth(), " +
                "or remove TapInAuthMethod.SmsOtp from Options.Methods."),
            sp.GetRequiredService<TokenHasher>(),
            sp.GetRequiredService<IRateLimiter>(),
            sp.GetRequiredService<IAuditSink>(),
            sp.GetRequiredService<TapInAuthClaimsPrincipalFactory>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<SmsOtpService>>()));

        services.AddHttpContextAccessor();

        // Admin policy — the role name comes from SecurityOptions.AdminRole (default "TapInAuthAdmin").
        // The admin Razor Pages in TapInAuth.UI decorate themselves with [Authorize(Policy = "TapInAuth.Admin")].
        services.AddAuthorization(authOpts =>
        {
            authOpts.AddPolicy("TapInAuth.Admin", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(ctx =>
                {
                    var sp = ctx.Resource as IServiceProvider
                          ?? (ctx.Resource is Microsoft.AspNetCore.Http.HttpContext h ? h.RequestServices : null);
                    var role = sp?.GetService<IOptions<TapInAuthOptions>>()?.Value.Security.AdminRole
                              ?? "TapInAuthAdmin";
                    return ctx.User.IsInRole(role);
                });
            });
        });

        // Wire FIDO2 / WebAuthn services — driven entirely from TapInAuthOptions.Relying.
        services.AddOptions<Fido2Configuration>().Configure<IOptions<TapInAuthOptions>>((fido, tapOpts) =>
        {
            var rp = tapOpts.Value.Relying;
            fido.ServerDomain = rp.Id ?? "localhost";
            fido.ServerName = rp.Name;
            fido.Origins = rp.AllowedOrigins.Count > 0
                ? new HashSet<string>(rp.AllowedOrigins, StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal) { "https://localhost:5001", "http://localhost:5000" };
            fido.TimestampDriftTolerance = 300_000; // 5 min
        });

        // IFido2 is cheap to construct (no I/O); register as singleton.
        services.TryAddSingleton<IFido2>(sp =>
        {
            var fidoOpts = sp.GetRequiredService<IOptions<Fido2Configuration>>().Value;
            return new Fido2(fidoOpts);
        });

        // PasskeyService depends on scoped credential store; register scoped.
        services.TryAddScoped<PasskeyService>();

        return new TapInAuthBuilder(services);
    }

    /// <summary>Register TapInAuth and bind options from a configuration section (e.g., <c>"TapInAuth"</c>).</summary>
    public static TapInAuthBuilder AddTapInAuth(this IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration section)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(section);
        services.AddOptions<TapInAuthOptions>().Bind(section);
        return services.AddTapInAuth();
    }

    /// <summary>Use the default cookie handoff against the host's configured default authentication scheme.</summary>
    public static TapInAuthBuilder UseCookieAuthenticationHandoff(this TapInAuthBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddSingleton<IAuthenticationHandoff, CookieAuthenticationHandoff>();
        return builder;
    }

    /// <summary>Plug in a custom <see cref="IAuthenticationHandoff"/> implementation.</summary>
    public static TapInAuthBuilder AddAuthenticationHandoff<THandoff>(this TapInAuthBuilder builder)
        where THandoff : class, IAuthenticationHandoff
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddSingleton<IAuthenticationHandoff, THandoff>();
        return builder;
    }

    /// <summary>Register a development-only console email sender (no real delivery — logs the email).</summary>
    public static TapInAuthBuilder AddConsoleEmail(this TapInAuthBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddSingleton<IEmailSender, ConsoleEmailSender>();
        return builder;
    }

    /// <summary>Register a custom <see cref="ITapInAuthUserStore"/>.</summary>
    public static TapInAuthBuilder AddCustomUserStore<TStore>(this TapInAuthBuilder builder)
        where TStore : class, ITapInAuthUserStore
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddScoped<ITapInAuthUserStore, TStore>();
        return builder;
    }

    /// <summary>Register a custom <see cref="ITenantResolver"/>.</summary>
    public static TapInAuthBuilder AddTenantResolver<TResolver>(this TapInAuthBuilder builder)
        where TResolver : class, ITenantResolver
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddScoped<ITenantResolver, TResolver>();
        return builder;
    }

    private static bool ValidateOptions(TapInAuthOptions options)
    {
        if (options.Methods == TapInAuthMethod.None)
        {
            return false;
        }
        if (options.Security.OtpDigits is < 4 or > 10)
        {
            return false;
        }
        if (options.Security.MagicLinkLifetime <= TimeSpan.Zero || options.Security.OtpLifetime <= TimeSpan.Zero)
        {
            return false;
        }
        return true;
    }
}

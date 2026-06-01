using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TapInAuth.AspNetCore.DependencyInjection;
using TapInAuth.Auditing;
using TapInAuth.Store.EntityFrameworkCore.Stores;
using TapInAuth.Stores;

namespace TapInAuth.Store.EntityFrameworkCore.DependencyInjection;

/// <summary>Add the EF Core store to a <see cref="TapInAuthBuilder"/>.</summary>
public static class TapInAuthEfCoreBuilderExtensions
{
    /// <summary>
    /// Register the EF Core implementations of the TapInAuth stores. Requires the host's
    /// <typeparamref name="TContext"/> to already be registered as a scoped service.
    /// The host's <c>OnModelCreating</c> should call <c>modelBuilder.ApplyTapInAuthConfiguration()</c>.
    /// </summary>
    public static TapInAuthBuilder AddEfCoreStore<TContext>(this TapInAuthBuilder builder) where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddScoped<ITapInAuthUserStore, EfCoreTapInAuthUserStore<TContext>>();
        builder.Services.AddScoped<IMagicLinkTokenStore, EfCoreMagicLinkTokenStore<TContext>>();
        builder.Services.AddScoped<IOtpCodeStore, EfCoreOtpCodeStore<TContext>>();
        builder.Services.AddScoped<ICredentialStore, EfCoreCredentialStore<TContext>>();
        builder.Services.AddScoped<IRecoveryCodeStore, EfCoreRecoveryCodeStore<TContext>>();
        return builder;
    }

    /// <summary>
    /// Use the EF Core <see cref="IAuditSink"/> + <see cref="IAuditQuery"/>. Replaces the default
    /// <c>LoggingAuditSink</c> with a persistent sink + read-side, enabling the built-in admin audit feed.
    /// </summary>
    public static TapInAuthBuilder AddEfCoreAuditSink<TContext>(this TapInAuthBuilder builder) where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.Replace(ServiceDescriptor.Scoped<IAuditSink, EfCoreAuditSink<TContext>>());
        builder.Services.AddScoped<IAuditQuery, EfCoreAuditSink<TContext>>();
        return builder;
    }
}

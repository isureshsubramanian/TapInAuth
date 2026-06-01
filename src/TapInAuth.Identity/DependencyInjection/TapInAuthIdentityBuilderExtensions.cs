using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using TapInAuth.AspNetCore.DependencyInjection;
using TapInAuth.Stores;

namespace TapInAuth.Identity.DependencyInjection;

/// <summary>Bridge TapInAuth into an existing ASP.NET Core Identity setup.</summary>
public static class TapInAuthIdentityBuilderExtensions
{
    /// <summary>
    /// Use ASP.NET Core Identity's user store as TapInAuth's <see cref="ITapInAuthUserStore"/>.
    /// </summary>
    /// <typeparam name="TUser">The host's <see cref="IdentityUser"/> type.</typeparam>
    /// <remarks>
    /// The host must have already called <c>services.AddIdentity&lt;TUser, ...&gt;()</c> (or
    /// <c>AddIdentityCore</c>) before this. The credential, magic-link, and OTP stores still come from
    /// <c>AddEfCoreStore&lt;TContext&gt;()</c> or your custom implementations.
    /// </remarks>
    public static TapInAuthBuilder AddIdentityAdapter<TUser>(this TapInAuthBuilder builder)
        where TUser : IdentityUser, new()
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddScoped<ITapInAuthUserStore, IdentityTapInAuthUserStore<TUser>>();
        return builder;
    }
}

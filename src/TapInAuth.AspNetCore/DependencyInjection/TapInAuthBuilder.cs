using Microsoft.Extensions.DependencyInjection;

namespace TapInAuth.AspNetCore.DependencyInjection;

/// <summary>
/// Fluent builder returned by <c>AddTapInAuth(...)</c>. Sub-packages (EF Core store, SMTP, etc.)
/// hang their <c>Add...</c> extension methods off this builder so the call chain reads naturally.
/// </summary>
public sealed class TapInAuthBuilder
{
    /// <summary>Construct a builder bound to the given service collection.</summary>
    public TapInAuthBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>The underlying service collection.</summary>
    public IServiceCollection Services { get; }
}

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TapInAuth.AspNetCore.DependencyInjection;
using TapInAuth.Auditing;
using TapInAuth.Core.Security;
using TapInAuth.Core.Services;
using TapInAuth.Handoff;
using TapInAuth.Options;
using TapInAuth.RateLimiting;
using TapInAuth.Tenancy;
using Xunit;

namespace TapInAuth.AspNetCore.Tests;

public class AddTapInAuthTests
{
    [Fact]
    public void Registers_required_singletons()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapInAuth(o => o.Methods = TapInAuthMethod.MagicLink);

        var sp = services.BuildServiceProvider();
        sp.GetRequiredService<IOptions<TapInAuthOptions>>().Should().NotBeNull();
        sp.GetRequiredService<TokenHasher>().Should().NotBeNull();
        sp.GetRequiredService<TapInAuthClaimsPrincipalFactory>().Should().NotBeNull();
        sp.GetRequiredService<IRateLimiter>().Should().NotBeNull();
        sp.GetRequiredService<IAuditSink>().Should().NotBeNull();
        sp.GetRequiredService<ITenantResolver>().Should().NotBeNull();
        sp.GetRequiredService<IAuthenticationHandoff>().Should().NotBeNull();
    }

    [Fact]
    public void Configure_callback_is_invoked()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapInAuth(o =>
        {
            o.Methods = TapInAuthMethod.EmailOtp;
            o.Theme.Accent = "#FF0000";
        });
        var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<IOptions<TapInAuthOptions>>().Value;
        opts.Methods.Should().Be(TapInAuthMethod.EmailOtp);
        opts.Theme.Accent.Should().Be("#FF0000");
    }

    [Fact]
    public async Task Default_tenant_resolver_returns_default_tenant()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapInAuth(o => o.Methods = TapInAuthMethod.MagicLink);
        var sp = services.BuildServiceProvider();
        var resolver = sp.GetRequiredService<ITenantResolver>();
        var tenant = await resolver.ResolveAsync();
        tenant.Should().NotBeNull();
        tenant!.Id.Should().Be(TenantContext.DefaultTenantId);
    }

    [Fact]
    public void Options_with_no_methods_fail_validation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapInAuth(o => o.Methods = TapInAuthMethod.None);
        var sp = services.BuildServiceProvider();
        var act = () => _ = sp.GetRequiredService<IOptions<TapInAuthOptions>>().Value;
        act.Should().Throw<OptionsValidationException>();
    }
}

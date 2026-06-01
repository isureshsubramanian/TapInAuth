using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TapInAuth.AspNetCore.DependencyInjection;
using TapInAuth.Options;
using Xunit;

namespace TapInAuth.AspNetCore.Tests;

public class OptionsValidationTests
{
    private static TapInAuthOptions Resolve(Action<TapInAuthOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapInAuth(configure);
        return services.BuildServiceProvider().GetRequiredService<IOptions<TapInAuthOptions>>().Value;
    }

    [Fact]
    public void Methods_None_fails_validation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapInAuth(o => o.Methods = TapInAuthMethod.None);
        var sp = services.BuildServiceProvider();
        var act = () => _ = sp.GetRequiredService<IOptions<TapInAuthOptions>>().Value;
        act.Should().Throw<OptionsValidationException>();
    }

    [Theory]
    [InlineData(3)]    // below 4 lower bound
    [InlineData(11)]   // above 10 upper bound
    [InlineData(0)]
    public void OtpDigits_outside_4_to_10_fails_validation(int digits)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapInAuth(o =>
        {
            o.Methods = TapInAuthMethod.EmailOtp;
            o.Security.OtpDigits = digits;
        });
        var sp = services.BuildServiceProvider();
        var act = () => _ = sp.GetRequiredService<IOptions<TapInAuthOptions>>().Value;
        act.Should().Throw<OptionsValidationException>();
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(10)]
    public void OtpDigits_within_range_passes(int digits)
    {
        var opts = Resolve(o =>
        {
            o.Methods = TapInAuthMethod.EmailOtp;
            o.Security.OtpDigits = digits;
        });
        opts.Security.OtpDigits.Should().Be(digits);
    }

    [Fact]
    public void Zero_or_negative_MagicLinkLifetime_fails_validation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapInAuth(o =>
        {
            o.Methods = TapInAuthMethod.MagicLink;
            o.Security.MagicLinkLifetime = TimeSpan.Zero;
        });
        var sp = services.BuildServiceProvider();
        var act = () => _ = sp.GetRequiredService<IOptions<TapInAuthOptions>>().Value;
        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Zero_or_negative_OtpLifetime_fails_validation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapInAuth(o =>
        {
            o.Methods = TapInAuthMethod.EmailOtp;
            o.Security.OtpLifetime = TimeSpan.FromSeconds(-1);
        });
        var sp = services.BuildServiceProvider();
        var act = () => _ = sp.GetRequiredService<IOptions<TapInAuthOptions>>().Value;
        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Default_options_are_valid()
    {
        var opts = Resolve(o => { /* take defaults except for required Methods bitmask */
            o.Methods = TapInAuthMethod.MagicLink | TapInAuthMethod.EmailOtp;
        });
        opts.Should().NotBeNull();
        opts.Security.OtpDigits.Should().Be(6);
        opts.Security.MagicLinkLifetime.Should().Be(TimeSpan.FromMinutes(10));
        opts.Security.OtpLifetime.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void SmsOtp_method_validates_when_provider_not_yet_registered()
    {
        // The build-time DI validator for SmsOtpService is a factory registration so a host can
        // declare Methods.SmsOtp without yet calling AddTwilioSms. The factory only throws at first
        // resolution if ISmsSender is missing — covered by separate runtime tests.
        var opts = Resolve(o => o.Methods = TapInAuthMethod.SmsOtp);
        opts.Methods.Should().HaveFlag(TapInAuthMethod.SmsOtp);
    }
}

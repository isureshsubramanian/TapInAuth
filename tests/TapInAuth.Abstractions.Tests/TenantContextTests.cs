using FluentAssertions;
using Xunit;

namespace TapInAuth.Abstractions.Tests;

public class TenantContextTests
{
    [Fact]
    public void Default_tenant_uses_well_known_id()
    {
        TenantContext.Default.Id.Should().Be("default");
        TenantContext.Default.Id.Should().Be(TenantContext.DefaultTenantId);
    }

    [Fact]
    public void Default_tenant_is_singleton_per_well_known_id()
    {
        var d1 = TenantContext.Default;
        var d2 = TenantContext.Default;
        ReferenceEquals(d1, d2).Should().BeTrue();
    }

    [Fact]
    public void With_block_creates_modified_copy()
    {
        var t = new TenantContext("acme", "Acme");
        var t2 = t with { ThemeAccent = "#FF0000" };
        t2.Id.Should().Be("acme");
        t2.DisplayName.Should().Be("Acme");
        t2.ThemeAccent.Should().Be("#FF0000");
        t.ThemeAccent.Should().BeNull();
    }
}

public class TapInAuthMethodTests
{
    [Fact]
    public void All_includes_every_method()
    {
        TapInAuthMethod.All.HasFlag(TapInAuthMethod.Passkey).Should().BeTrue();
        TapInAuthMethod.All.HasFlag(TapInAuthMethod.MagicLink).Should().BeTrue();
        TapInAuthMethod.All.HasFlag(TapInAuthMethod.EmailOtp).Should().BeTrue();
        TapInAuthMethod.All.HasFlag(TapInAuthMethod.SmsOtp).Should().BeTrue();
    }

    [Fact]
    public void None_has_no_flags()
    {
        TapInAuthMethod.None.HasFlag(TapInAuthMethod.Passkey).Should().BeFalse();
        ((int)TapInAuthMethod.None).Should().Be(0);
    }

    [Fact]
    public void Flags_combine_with_or()
    {
        var combo = TapInAuthMethod.MagicLink | TapInAuthMethod.EmailOtp;
        combo.HasFlag(TapInAuthMethod.MagicLink).Should().BeTrue();
        combo.HasFlag(TapInAuthMethod.EmailOtp).Should().BeTrue();
        combo.HasFlag(TapInAuthMethod.Passkey).Should().BeFalse();
    }
}

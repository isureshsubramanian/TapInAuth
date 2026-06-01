using System.Security.Claims;
using FluentAssertions;
using TapInAuth.Claims;
using TapInAuth.Core.Services;
using Xunit;

namespace TapInAuth.Core.Tests.Services;

public class ClaimsPrincipalFactoryTests
{
    private static readonly TapInAuthUser SampleUser = new(
        Id: Guid.NewGuid(),
        TenantId: "acme",
        Email: "alice@acme.com",
        EmailVerified: true,
        CreatedAt: DateTimeOffset.UtcNow,
        DisplayName: "Alice");

    [Fact]
    public void Create_includes_required_claims()
    {
        var factory = new TapInAuthClaimsPrincipalFactory();
        var principal = factory.Create(SampleUser, new TenantContext("acme"), TapInAuthMethod.MagicLink, DateTimeOffset.UtcNow);

        principal.Identity.Should().NotBeNull();
        principal.Identity!.IsAuthenticated.Should().BeTrue();
        principal.FindFirst(ClaimTypes.NameIdentifier)?.Value.Should().Be(SampleUser.Id.ToString("D"));
        principal.FindFirst(ClaimTypes.Email)?.Value.Should().Be(SampleUser.Email);
        principal.FindFirst(TapInAuthClaimTypes.Tenant)?.Value.Should().Be("acme");
        principal.FindFirst(TapInAuthClaimTypes.Amr)?.Value.Should().Be("magiclink");
        principal.FindFirst(TapInAuthClaimTypes.EmailVerified)?.Value.Should().Be("true");
    }

    [Theory]
    [InlineData(TapInAuthMethod.Passkey,   "passkey")]
    [InlineData(TapInAuthMethod.MagicLink, "magiclink")]
    [InlineData(TapInAuthMethod.EmailOtp,  "emailotp")]
    [InlineData(TapInAuthMethod.SmsOtp,    "smsotp")]
    public void Amr_maps_to_known_value(TapInAuthMethod method, string expected)
    {
        var factory = new TapInAuthClaimsPrincipalFactory();
        var principal = factory.Create(SampleUser, new TenantContext("t"), method, DateTimeOffset.UtcNow);
        principal.FindFirst(TapInAuthClaimTypes.Amr)?.Value.Should().Be(expected);
    }
}

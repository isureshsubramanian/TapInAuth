using FluentAssertions;
using TapInAuth.Core.Security;
using Xunit;

namespace TapInAuth.Core.Tests.Security;

public class TokenGeneratorTests
{
    [Fact]
    public void GenerateMagicLinkToken_default_length_is_url_safe()
    {
        var token = TokenGenerator.GenerateMagicLinkToken();
        token.Should().NotBeNullOrEmpty();
        token.Should().NotContain("+").And.NotContain("/").And.NotContain("=");
        // 32 random bytes → 43 base64url chars
        token.Length.Should().Be(43);
    }

    [Fact]
    public void GenerateMagicLinkToken_below_minimum_throws()
    {
        var act = () => TokenGenerator.GenerateMagicLinkToken(8);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GenerateMagicLinkToken_produces_unique_values()
    {
        var values = Enumerable.Range(0, 1000).Select(_ => TokenGenerator.GenerateMagicLinkToken()).ToHashSet();
        values.Count.Should().Be(1000);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(10)]
    public void GenerateOtp_returns_correct_digit_count(int digits)
    {
        var code = TokenGenerator.GenerateOtp(digits);
        code.Length.Should().Be(digits);
        code.Should().MatchRegex("^[0-9]+$");
    }

    [Theory]
    [InlineData(3)]
    [InlineData(11)]
    public void GenerateOtp_outside_range_throws(int digits)
    {
        var act = () => TokenGenerator.GenerateOtp(digits);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Base64Url_round_trips()
    {
        var data = new byte[] { 1, 2, 3, 4, 5, 0xFE, 0xFF };
        var encoded = TokenGenerator.Base64UrlEncode(data);
        encoded.Should().NotContain("+").And.NotContain("/").And.NotContain("=");
        var decoded = TokenGenerator.Base64UrlDecode(encoded);
        decoded.Should().Equal(data);
    }
}

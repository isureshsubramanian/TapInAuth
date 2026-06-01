using FluentAssertions;
using Xunit;

namespace TapInAuth.Abstractions.Tests;

public class PhoneNumberTests
{
    [Theory]
    [InlineData("+14155550100",        "+14155550100")]
    [InlineData("+1 415 555 0100",     "+14155550100")]
    [InlineData("+1 (415) 555-0100",   "+14155550100")]
    [InlineData("+1.415.555.0100",     "+14155550100")]
    [InlineData("+44 20 7946 0958",    "+442079460958")]
    [InlineData("+86 10 1234 5678",    "+861012345678")]
    public void Strips_separators_and_keeps_plus_prefix(string raw, string expected)
    {
        TapInAuth.PhoneNumber.TryNormalize(raw, out var n).Should().BeTrue();
        n.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_null_or_whitespace(string? raw)
    {
        TapInAuth.PhoneNumber.TryNormalize(raw, out var n).Should().BeFalse();
        n.Should().BeEmpty();
    }

    [Theory]
    [InlineData("14155550100")]            // missing leading +
    [InlineData("415-555-0100")]           // missing +
    [InlineData("(415) 555-0100")]         // missing +
    public void Rejects_input_without_leading_plus(string raw)
    {
        TapInAuth.PhoneNumber.TryNormalize(raw, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("+1 234")]                 // 4 digits — too short
    [InlineData("+1")]                     // 1 digit
    [InlineData("+1234567")]               // 7 digits — under 8 minimum
    public void Rejects_too_short(string raw)
    {
        TapInAuth.PhoneNumber.TryNormalize(raw, out _).Should().BeFalse();
    }

    [Fact]
    public void Rejects_too_long_above_E164_cap()
    {
        // E.164 caps at 15 digits; 16 should fail.
        var raw = "+" + new string('1', 16);
        TapInAuth.PhoneNumber.TryNormalize(raw, out _).Should().BeFalse();
    }

    [Fact]
    public void Accepts_exact_E164_max_length()
    {
        // 15 digits — the E.164 max.
        var raw = "+" + new string('1', 15);
        TapInAuth.PhoneNumber.TryNormalize(raw, out var n).Should().BeTrue();
        n.Should().Be(raw);
    }

    [Fact]
    public void Plus_after_digits_is_treated_as_a_separator_and_dropped()
    {
        // A "+" in any position other than first is dropped silently; the result is still all digits
        // after the initial +. We don't want to accept "+1+4155550100" as valid — the second + becomes
        // garbage, and because the first slot already consumed the +, the result is "+14155550100".
        TapInAuth.PhoneNumber.TryNormalize("+1+4155550100", out var n).Should().BeTrue();
        n.Should().Be("+14155550100");
    }

    [Fact]
    public void Letters_and_symbols_are_silently_dropped()
    {
        // Trying to be permissive about cosmetic input — but the normalized form is still just + and digits.
        TapInAuth.PhoneNumber.TryNormalize("+1-abc-415-555-0100xyz", out var n).Should().BeTrue();
        n.Should().Be("+14155550100");
    }

    [Fact]
    public void Idempotent_on_already_normalized_input()
    {
        const string raw = "+14155550100";
        TapInAuth.PhoneNumber.TryNormalize(raw, out var once).Should().BeTrue();
        TapInAuth.PhoneNumber.TryNormalize(once, out var twice).Should().BeTrue();
        twice.Should().Be(once);
    }
}

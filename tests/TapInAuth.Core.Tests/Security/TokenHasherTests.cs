using FluentAssertions;
using TapInAuth.Core.Security;
using Xunit;

namespace TapInAuth.Core.Tests.Security;

public class TokenHasherTests
{
    private static TokenHasher CreateHasher(byte[]? pepper = null)
    {
        pepper ??= new byte[32];
        Array.Fill(pepper, (byte)0x42);
        return new TokenHasher(pepper);
    }

    [Fact]
    public void Hash_is_deterministic_for_same_input_and_pepper()
    {
        var hasher = CreateHasher();
        var h1 = hasher.Hash("abc");
        var h2 = hasher.Hash("abc");
        h1.Should().Equal(h2);
    }

    [Fact]
    public void Hash_differs_for_different_input()
    {
        var hasher = CreateHasher();
        var h1 = hasher.Hash("abc");
        var h2 = hasher.Hash("abd");
        h1.Should().NotEqual(h2);
    }

    [Fact]
    public void Hash_differs_under_different_pepper()
    {
        var p1 = new byte[32]; Array.Fill(p1, (byte)0xAA);
        var p2 = new byte[32]; Array.Fill(p2, (byte)0xBB);
        var h1 = new TokenHasher(p1).Hash("abc");
        var h2 = new TokenHasher(p2).Hash("abc");
        h1.Should().NotEqual(h2);
    }

    [Fact]
    public void FixedTimeEquals_returns_true_for_equal_arrays()
    {
        var a = new byte[] { 1, 2, 3, 4 };
        var b = new byte[] { 1, 2, 3, 4 };
        TokenHasher.FixedTimeEquals(a, b).Should().BeTrue();
    }

    [Fact]
    public void FixedTimeEquals_returns_false_for_different_arrays()
    {
        var a = new byte[] { 1, 2, 3, 4 };
        var b = new byte[] { 1, 2, 3, 5 };
        TokenHasher.FixedTimeEquals(a, b).Should().BeFalse();
    }

    [Fact]
    public void FixedTimeEquals_returns_false_for_different_lengths()
    {
        var a = new byte[] { 1, 2, 3 };
        var b = new byte[] { 1, 2, 3, 4 };
        TokenHasher.FixedTimeEquals(a, b).Should().BeFalse();
    }

    [Fact]
    public void Short_pepper_throws()
    {
        var act = () => _ = new TokenHasher(new byte[16]);
        act.Should().Throw<ArgumentException>();
    }
}

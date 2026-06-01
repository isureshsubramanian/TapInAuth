using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using TapInAuth.Core.RateLimiting;
using Xunit;

namespace TapInAuth.Core.Tests.RateLimiting;

public class InMemoryRateLimiterTests
{
    [Fact]
    public async Task Allows_up_to_limit_then_blocks()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var rl = new InMemoryRateLimiter(TimeSpan.FromMinutes(1), limit: 3, timeProvider: time);

        (await rl.TryAcquireAsync("k")).Should().BeTrue();
        (await rl.TryAcquireAsync("k")).Should().BeTrue();
        (await rl.TryAcquireAsync("k")).Should().BeTrue();
        (await rl.TryAcquireAsync("k")).Should().BeFalse(); // 4th — denied
    }

    [Fact]
    public async Task Permits_recover_after_window_advances()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var rl = new InMemoryRateLimiter(TimeSpan.FromMinutes(1), limit: 1, timeProvider: time);

        (await rl.TryAcquireAsync("k")).Should().BeTrue();
        (await rl.TryAcquireAsync("k")).Should().BeFalse();

        time.Advance(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(1));

        (await rl.TryAcquireAsync("k")).Should().BeTrue();
    }

    [Fact]
    public async Task Keys_are_independent()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var rl = new InMemoryRateLimiter(TimeSpan.FromMinutes(1), limit: 1, timeProvider: time);

        (await rl.TryAcquireAsync("a")).Should().BeTrue();
        (await rl.TryAcquireAsync("a")).Should().BeFalse();
        (await rl.TryAcquireAsync("b")).Should().BeTrue();
    }

    [Fact]
    public void Constructor_validates_arguments()
    {
        var time = new FakeTimeProvider();
        var actNegWindow = () => new InMemoryRateLimiter(TimeSpan.Zero, 1, time);
        var actNegLimit = () => new InMemoryRateLimiter(TimeSpan.FromSeconds(1), 0, time);
        actNegWindow.Should().Throw<ArgumentOutOfRangeException>();
        actNegLimit.Should().Throw<ArgumentOutOfRangeException>();
    }
}

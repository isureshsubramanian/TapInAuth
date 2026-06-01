using TapInAuth.RateLimiting;

namespace TapInAuth.Core.Tests.Services.Fakes;

/// <summary>Simple rate-limiter fake — flip <see cref="AllowAcquire"/> to deny.</summary>
public sealed class FakeRateLimiter : IRateLimiter
{
    public bool AllowAcquire { get; set; } = true;

    public List<string> AcquireKeys { get; } = new();

    public ValueTask<bool> TryAcquireAsync(string key, int permits = 1, CancellationToken cancellationToken = default)
    {
        AcquireKeys.Add(key);
        return ValueTask.FromResult(AllowAcquire);
    }
}

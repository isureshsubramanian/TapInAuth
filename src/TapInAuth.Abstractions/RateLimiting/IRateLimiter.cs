namespace TapInAuth.RateLimiting;

/// <summary>
/// Per-key rate limiter used by TapInAuth to throttle sign-in attempts, magic-link issuance, and OTP entry.
/// Keys are arbitrary strings (e.g., <c>"magiclink:tenantA:user@x.com"</c>, <c>"otp:tenantA:1.2.3.4"</c>).
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Attempt to acquire a permit for the given key. Returns true if allowed; false if throttled.
    /// </summary>
    /// <param name="key">Bucket key. Caller is responsible for namespacing (tenant + operation + identifier).</param>
    /// <param name="permits">Number of permits to consume (default 1).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<bool> TryAcquireAsync(string key, int permits = 1, CancellationToken cancellationToken = default);
}

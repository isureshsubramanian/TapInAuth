using System.Collections.Concurrent;
using TapInAuth.RateLimiting;

namespace TapInAuth.Core.RateLimiting;

/// <summary>
/// In-process sliding-window rate limiter. Suitable for single-instance deployments and tests;
/// production multi-instance deployments should swap in a distributed implementation (Redis, etc.)
/// behind <see cref="IRateLimiter"/>.
/// </summary>
public sealed class InMemoryRateLimiter : IRateLimiter
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _window;
    private readonly int _limit;
    private readonly ConcurrentDictionary<string, Bucket> _buckets = new(StringComparer.Ordinal);

    /// <summary>Create a limiter with the given window and permit count.</summary>
    /// <param name="window">Window over which permits are counted.</param>
    /// <param name="limit">Maximum permits per window per key.</param>
    /// <param name="timeProvider">Time source. Defaults to <see cref="TimeProvider.System"/>.</param>
    public InMemoryRateLimiter(TimeSpan window, int limit, TimeProvider? timeProvider = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(window, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        _window = window;
        _limit = limit;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public ValueTask<bool> TryAcquireAsync(string key, int permits = 1, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(permits);

        var now = _timeProvider.GetUtcNow();
        var bucket = _buckets.GetOrAdd(key, _ => new Bucket());
        lock (bucket)
        {
            bucket.Prune(now - _window);
            if (bucket.Count + permits > _limit)
            {
                return ValueTask.FromResult(false);
            }
            for (var i = 0; i < permits; i++)
            {
                bucket.Add(now);
            }
            return ValueTask.FromResult(true);
        }
    }

    private sealed class Bucket
    {
        // Ring of timestamps; simple Queue<T> is fine for the modest sizes we see in practice.
        private readonly Queue<DateTimeOffset> _hits = new();

        public int Count => _hits.Count;

        public void Add(DateTimeOffset ts) => _hits.Enqueue(ts);

        public void Prune(DateTimeOffset cutoff)
        {
            while (_hits.Count > 0 && _hits.Peek() < cutoff)
            {
                _hits.Dequeue();
            }
        }
    }
}

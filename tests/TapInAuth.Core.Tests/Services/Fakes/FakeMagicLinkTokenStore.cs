using TapInAuth.Stores;
using TapInAuth.Tokens;

namespace TapInAuth.Core.Tests.Services.Fakes;

/// <summary>In-memory <see cref="IMagicLinkTokenStore"/>. Saved tokens stay accessible after consumption.</summary>
public sealed class FakeMagicLinkTokenStore : IMagicLinkTokenStore
{
    public List<MagicLinkToken> Saved { get; } = new();

    public Task SaveAsync(MagicLinkToken token, CancellationToken cancellationToken = default)
    {
        Saved.Add(token);
        return Task.CompletedTask;
    }

    public Task<MagicLinkToken?> FindByIdAsync(TenantContext tenant, Guid tokenId, CancellationToken cancellationToken = default)
    {
        var match = Saved.FirstOrDefault(t => t.Id == tokenId && t.TenantId == tenant.Id);
        return Task.FromResult<MagicLinkToken?>(match);
    }

    public Task MarkConsumedAsync(TenantContext tenant, Guid tokenId, DateTimeOffset consumedAt, CancellationToken cancellationToken = default)
    {
        var t = Saved.FirstOrDefault(x => x.Id == tokenId && x.TenantId == tenant.Id);
        if (t is not null)
        {
            t.ConsumedAt = consumedAt;
        }
        return Task.CompletedTask;
    }

    public Task<int> PurgeExpiredAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default)
    {
        var removed = Saved.RemoveAll(t => t.ExpiresAt < cutoff);
        return Task.FromResult(removed);
    }
}

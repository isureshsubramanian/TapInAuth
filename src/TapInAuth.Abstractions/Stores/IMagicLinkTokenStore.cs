using TapInAuth.Tokens;

namespace TapInAuth.Stores;

/// <summary>
/// Storage for magic-link tokens. All operations are tenant-scoped.
/// </summary>
public interface IMagicLinkTokenStore
{
    /// <summary>Persist a freshly issued magic-link token.</summary>
    Task SaveAsync(MagicLinkToken token, CancellationToken cancellationToken = default);

    /// <summary>Look up a token by its public ID within the tenant. Returns null if not found.</summary>
    Task<MagicLinkToken?> FindByIdAsync(TenantContext tenant, Guid tokenId, CancellationToken cancellationToken = default);

    /// <summary>Mark a token as consumed (idempotent — if already consumed, no-op).</summary>
    Task MarkConsumedAsync(TenantContext tenant, Guid tokenId, DateTimeOffset consumedAt, CancellationToken cancellationToken = default);

    /// <summary>Hard-delete tokens that expired before <paramref name="cutoff"/>. Returns number deleted.</summary>
    Task<int> PurgeExpiredAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default);
}

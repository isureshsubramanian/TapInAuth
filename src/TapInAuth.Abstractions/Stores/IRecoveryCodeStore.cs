using TapInAuth.Tokens;

namespace TapInAuth.Stores;

/// <summary>Storage for one-time recovery codes. All operations are tenant-scoped.</summary>
public interface IRecoveryCodeStore
{
    /// <summary>Persist a batch of freshly generated recovery codes.</summary>
    Task SaveBatchAsync(IReadOnlyList<RecoveryCode> codes, CancellationToken cancellationToken = default);

    /// <summary>List all unconsumed recovery codes for a user. Caller iterates and compares hashes.</summary>
    Task<IReadOnlyList<RecoveryCode>> ListActiveAsync(TenantContext tenant, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Mark a recovery code consumed. Idempotent.</summary>
    Task MarkConsumedAsync(TenantContext tenant, Guid codeId, DateTimeOffset consumedAt, CancellationToken cancellationToken = default);

    /// <summary>Delete ALL of a user's recovery codes (both used and unused). Called before regenerating.</summary>
    Task<int> DeleteAllForUserAsync(TenantContext tenant, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Count the unconsumed recovery codes a user has remaining.</summary>
    Task<int> CountActiveAsync(TenantContext tenant, Guid userId, CancellationToken cancellationToken = default);
}

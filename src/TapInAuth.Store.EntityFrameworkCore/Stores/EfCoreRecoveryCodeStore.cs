using Microsoft.EntityFrameworkCore;
using TapInAuth.Store.EntityFrameworkCore.Entities;
using TapInAuth.Stores;
using TapInAuth.Tokens;

namespace TapInAuth.Store.EntityFrameworkCore.Stores;

/// <summary>EF Core implementation of <see cref="IRecoveryCodeStore"/>.</summary>
public sealed class EfCoreRecoveryCodeStore<TContext> : IRecoveryCodeStore where TContext : DbContext
{
    private readonly TContext _db;

    /// <summary>Construct the store.</summary>
    public EfCoreRecoveryCodeStore(TContext db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc />
    public async Task SaveBatchAsync(IReadOnlyList<RecoveryCode> codes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(codes);
        foreach (var code in codes)
        {
            _db.Set<RecoveryCodeEntity>().Add(new RecoveryCodeEntity
            {
                Id = code.Id,
                TenantId = code.TenantId,
                UserId = code.UserId,
                CodeHash = code.CodeHash,
                CreatedAt = code.CreatedAt,
                ConsumedAt = code.ConsumedAt,
            });
        }
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RecoveryCode>> ListActiveAsync(TenantContext tenant, Guid userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        var rows = await _db.Set<RecoveryCodeEntity>()
            .AsNoTracking()
            .Where(x => x.TenantId == tenant.Id && x.UserId == userId && x.ConsumedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(Map).ToList();
    }

    /// <inheritdoc />
    public async Task MarkConsumedAsync(TenantContext tenant, Guid codeId, DateTimeOffset consumedAt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        var entity = await _db.Set<RecoveryCodeEntity>()
            .FirstOrDefaultAsync(x => x.TenantId == tenant.Id && x.Id == codeId, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null || entity.ConsumedAt is not null)
        {
            return;
        }
        entity.ConsumedAt = consumedAt;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> DeleteAllForUserAsync(TenantContext tenant, Guid userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        return await _db.Set<RecoveryCodeEntity>()
            .Where(x => x.TenantId == tenant.Id && x.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> CountActiveAsync(TenantContext tenant, Guid userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        return await _db.Set<RecoveryCodeEntity>()
            .AsNoTracking()
            .CountAsync(x => x.TenantId == tenant.Id && x.UserId == userId && x.ConsumedAt == null, cancellationToken)
            .ConfigureAwait(false);
    }

    private static RecoveryCode Map(RecoveryCodeEntity e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        UserId = e.UserId,
        CodeHash = e.CodeHash,
        CreatedAt = e.CreatedAt,
        ConsumedAt = e.ConsumedAt,
    };
}

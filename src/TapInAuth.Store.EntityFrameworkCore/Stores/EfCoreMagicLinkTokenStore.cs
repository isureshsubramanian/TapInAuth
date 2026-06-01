using Microsoft.EntityFrameworkCore;
using TapInAuth.Store.EntityFrameworkCore.Entities;
using TapInAuth.Stores;
using TapInAuth.Tokens;

namespace TapInAuth.Store.EntityFrameworkCore.Stores;

/// <summary>EF Core implementation of <see cref="IMagicLinkTokenStore"/>.</summary>
public sealed class EfCoreMagicLinkTokenStore<TContext> : IMagicLinkTokenStore where TContext : DbContext
{
    private readonly TContext _db;

    /// <summary>Construct the store.</summary>
    public EfCoreMagicLinkTokenStore(TContext db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc />
    public async Task SaveAsync(MagicLinkToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        _db.Set<MagicLinkTokenEntity>().Add(new MagicLinkTokenEntity
        {
            Id = token.Id,
            TenantId = token.TenantId,
            UserId = token.UserId,
            Email = token.Email,
            TokenHash = token.TokenHash,
            CreatedAt = token.CreatedAt,
            ExpiresAt = token.ExpiresAt,
            ConsumedAt = token.ConsumedAt,
            ReturnUrl = token.ReturnUrl,
        });
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MagicLinkToken?> FindByIdAsync(TenantContext tenant, Guid tokenId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        var e = await _db.Set<MagicLinkTokenEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenant.Id && x.Id == tokenId, cancellationToken)
            .ConfigureAwait(false);
        if (e is null)
        {
            return null;
        }
        return new MagicLinkToken
        {
            Id = e.Id,
            TenantId = e.TenantId,
            UserId = e.UserId,
            Email = e.Email,
            TokenHash = e.TokenHash,
            CreatedAt = e.CreatedAt,
            ExpiresAt = e.ExpiresAt,
            ConsumedAt = e.ConsumedAt,
            ReturnUrl = e.ReturnUrl,
        };
    }

    /// <inheritdoc />
    public async Task MarkConsumedAsync(TenantContext tenant, Guid tokenId, DateTimeOffset consumedAt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        var entity = await _db.Set<MagicLinkTokenEntity>()
            .FirstOrDefaultAsync(x => x.TenantId == tenant.Id && x.Id == tokenId, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null || entity.ConsumedAt is not null)
        {
            return;
        }
        entity.ConsumedAt = consumedAt;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> PurgeExpiredAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default)
    {
        return await _db.Set<MagicLinkTokenEntity>()
            .Where(x => x.ExpiresAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

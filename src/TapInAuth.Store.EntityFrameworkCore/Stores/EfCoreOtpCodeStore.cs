using Microsoft.EntityFrameworkCore;
using TapInAuth.Store.EntityFrameworkCore.Entities;
using TapInAuth.Stores;
using TapInAuth.Tokens;

namespace TapInAuth.Store.EntityFrameworkCore.Stores;

/// <summary>EF Core implementation of <see cref="IOtpCodeStore"/>.</summary>
public sealed class EfCoreOtpCodeStore<TContext> : IOtpCodeStore where TContext : DbContext
{
    private readonly TContext _db;

    /// <summary>Construct the store.</summary>
    public EfCoreOtpCodeStore(TContext db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc />
    public async Task SaveAsync(OtpCode otp, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(otp);
        _db.Set<OtpCodeEntity>().Add(new OtpCodeEntity
        {
            Id = otp.Id,
            TenantId = otp.TenantId,
            UserId = otp.UserId,
            Destination = otp.Destination,
            Channel = otp.Channel,
            CodeHash = otp.CodeHash,
            CreatedAt = otp.CreatedAt,
            ExpiresAt = otp.ExpiresAt,
            AttemptCount = otp.AttemptCount,
            ConsumedAt = otp.ConsumedAt,
        });
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OtpCode?> FindActiveAsync(TenantContext tenant, Guid userId, OtpChannel channel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        var e = await _db.Set<OtpCodeEntity>()
            .AsNoTracking()
            .Where(x => x.TenantId == tenant.Id && x.UserId == userId && x.Channel == channel && x.ConsumedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (e is null)
        {
            return null;
        }
        return new OtpCode
        {
            Id = e.Id,
            TenantId = e.TenantId,
            UserId = e.UserId,
            Destination = e.Destination,
            Channel = e.Channel,
            CodeHash = e.CodeHash,
            CreatedAt = e.CreatedAt,
            ExpiresAt = e.ExpiresAt,
            AttemptCount = e.AttemptCount,
            ConsumedAt = e.ConsumedAt,
        };
    }

    /// <inheritdoc />
    public async Task MarkConsumedAsync(TenantContext tenant, Guid otpId, DateTimeOffset consumedAt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        var entity = await _db.Set<OtpCodeEntity>()
            .FirstOrDefaultAsync(x => x.TenantId == tenant.Id && x.Id == otpId, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null || entity.ConsumedAt is not null)
        {
            return;
        }
        entity.ConsumedAt = consumedAt;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> IncrementAttemptAsync(TenantContext tenant, Guid otpId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        var entity = await _db.Set<OtpCodeEntity>()
            .FirstOrDefaultAsync(x => x.TenantId == tenant.Id && x.Id == otpId, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return 0;
        }
        entity.AttemptCount += 1;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity.AttemptCount;
    }

    /// <inheritdoc />
    public async Task<int> PurgeExpiredAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default)
    {
        return await _db.Set<OtpCodeEntity>()
            .Where(x => x.ExpiresAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

using Microsoft.EntityFrameworkCore;
using TapInAuth.Store.EntityFrameworkCore.Entities;
using TapInAuth.Stores;

namespace TapInAuth.Store.EntityFrameworkCore.Stores;

/// <summary>EF Core implementation of <see cref="ITapInAuthUserStore"/>.</summary>
public sealed class EfCoreTapInAuthUserStore<TContext> : ITapInAuthUserStore where TContext : DbContext
{
    private readonly TContext _db;
    private readonly TimeProvider _timeProvider;

    /// <summary>Construct the store.</summary>
    public EfCoreTapInAuthUserStore(TContext db, TimeProvider timeProvider)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public async Task<TapInAuthUser?> FindByEmailAsync(TenantContext tenant, string email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        var normalized = email.Trim().ToLowerInvariant();
        var e = await _db.Set<TapInAuthUserEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenant.Id && x.Email == normalized, cancellationToken)
            .ConfigureAwait(false);
        return e is null ? null : Map(e);
    }

    /// <inheritdoc />
    public async Task<TapInAuthUser?> FindByIdAsync(TenantContext tenant, Guid userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        var e = await _db.Set<TapInAuthUserEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenant.Id && x.Id == userId, cancellationToken)
            .ConfigureAwait(false);
        return e is null ? null : Map(e);
    }

    /// <inheritdoc />
    public async Task<TapInAuthUser> CreateAsync(TenantContext tenant, string email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        var entity = new TapInAuthUserEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Email = email.Trim().ToLowerInvariant(),
            EmailVerified = false,
            CreatedAt = _timeProvider.GetUtcNow(),
        };
        _db.Set<TapInAuthUserEntity>().Add(entity);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(entity);
    }

    /// <inheritdoc />
    public async Task SetEmailVerifiedAsync(TenantContext tenant, Guid userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        var entity = await _db.Set<TapInAuthUserEntity>()
            .FirstOrDefaultAsync(x => x.TenantId == tenant.Id && x.Id == userId, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null || entity.EmailVerified)
        {
            return;
        }
        entity.EmailVerified = true;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TapInAuthUser?> FindByPhoneAsync(TenantContext tenant, string phone, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        if (!PhoneNumber.TryNormalize(phone, out var normalized))
        {
            return null;
        }
        var e = await _db.Set<TapInAuthUserEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenant.Id && x.Phone == normalized, cancellationToken)
            .ConfigureAwait(false);
        return e is null ? null : Map(e);
    }

    /// <inheritdoc />
    public async Task SetPhoneAsync(TenantContext tenant, Guid userId, string? phone, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        var entity = await _db.Set<TapInAuthUserEntity>()
            .FirstOrDefaultAsync(x => x.TenantId == tenant.Id && x.Id == userId, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return;
        }

        string? normalized = null;
        if (!string.IsNullOrWhiteSpace(phone))
        {
            if (!PhoneNumber.TryNormalize(phone, out var n))
            {
                throw new ArgumentException("Phone number is not a valid E.164 number.", nameof(phone));
            }
            normalized = n;
        }

        if (entity.Phone == normalized)
        {
            return;
        }

        entity.Phone = normalized;
        // Any change to phone — including clearing it — resets the verified flag. Re-verification is mandatory.
        entity.PhoneVerified = false;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetPhoneVerifiedAsync(TenantContext tenant, Guid userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        var entity = await _db.Set<TapInAuthUserEntity>()
            .FirstOrDefaultAsync(x => x.TenantId == tenant.Id && x.Id == userId, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null || entity.PhoneVerified || entity.Phone is null)
        {
            return;
        }
        entity.PhoneVerified = true;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static TapInAuthUser Map(TapInAuthUserEntity e) =>
        new(e.Id, e.TenantId, e.Email, e.EmailVerified, e.CreatedAt, e.DisplayName, e.Phone, e.PhoneVerified);
}

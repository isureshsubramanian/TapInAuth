using Microsoft.EntityFrameworkCore;
using TapInAuth.Credentials;
using TapInAuth.Store.EntityFrameworkCore.Entities;
using TapInAuth.Stores;

namespace TapInAuth.Store.EntityFrameworkCore.Stores;

/// <summary>EF Core implementation of <see cref="ICredentialStore"/> (passkey credentials).</summary>
public sealed class EfCoreCredentialStore<TContext> : ICredentialStore where TContext : DbContext
{
    private readonly TContext _db;

    /// <summary>Construct the store.</summary>
    public EfCoreCredentialStore(TContext db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc />
    public async Task SaveAsync(Credential credential, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);
        _db.Set<CredentialEntity>().Add(new CredentialEntity
        {
            Id = credential.Id,
            TenantId = credential.TenantId,
            UserId = credential.UserId,
            CredentialId = credential.CredentialId,
            PublicKey = credential.PublicKey,
            SignatureCounter = credential.SignatureCounter,
            Aaguid = credential.Aaguid,
            DeviceName = credential.DeviceName,
            CreatedAt = credential.CreatedAt,
            LastUsedAt = credential.LastUsedAt,
        });
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Credential?> FindByCredentialIdAsync(TenantContext tenant, byte[] credentialId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(credentialId);

        // EF can't translate sequence-equality on byte[] across all providers; fetch candidates by tenant + length, compare in-memory.
        // For small per-tenant credential counts this is fine; production stores can override with a provider-specific binary equality.
        var candidates = await _db.Set<CredentialEntity>()
            .AsNoTracking()
            .Where(x => x.TenantId == tenant.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var match = candidates.FirstOrDefault(x => ByteArrayEquals(x.CredentialId, credentialId));
        return match is null ? null : Map(match);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Credential>> ListForUserAsync(TenantContext tenant, Guid userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        var rows = await _db.Set<CredentialEntity>()
            .AsNoTracking()
            .Where(x => x.TenantId == tenant.Id && x.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(Map).ToList();
    }

    /// <inheritdoc />
    public async Task UpdateAfterUseAsync(TenantContext tenant, Guid credentialId, uint signatureCounter, DateTimeOffset lastUsedAt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        var entity = await _db.Set<CredentialEntity>()
            .FirstOrDefaultAsync(x => x.TenantId == tenant.Id && x.Id == credentialId, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return;
        }
        entity.SignatureCounter = signatureCounter;
        entity.LastUsedAt = lastUsedAt;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(TenantContext tenant, Guid credentialId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        await _db.Set<CredentialEntity>()
            .Where(x => x.TenantId == tenant.Id && x.Id == credentialId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static Credential Map(CredentialEntity e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        UserId = e.UserId,
        CredentialId = e.CredentialId,
        PublicKey = e.PublicKey,
        SignatureCounter = (uint)e.SignatureCounter,
        Aaguid = e.Aaguid,
        DeviceName = e.DeviceName,
        CreatedAt = e.CreatedAt,
        LastUsedAt = e.LastUsedAt,
    };

    private static bool ByteArrayEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }
        for (var i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }
        return true;
    }
}

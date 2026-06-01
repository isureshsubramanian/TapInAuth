using TapInAuth.Credentials;

namespace TapInAuth.Stores;

/// <summary>
/// Storage for WebAuthn passkey credentials. Used from 0.3.
/// Defined now so the schema is correct from day one and no migration is needed.
/// </summary>
public interface ICredentialStore
{
    /// <summary>Persist a new credential.</summary>
    Task SaveAsync(Credential credential, CancellationToken cancellationToken = default);

    /// <summary>Find a credential by its raw WebAuthn credential ID within the tenant.</summary>
    Task<Credential?> FindByCredentialIdAsync(TenantContext tenant, byte[] credentialId, CancellationToken cancellationToken = default);

    /// <summary>List all credentials registered to a user in the tenant.</summary>
    Task<IReadOnlyList<Credential>> ListForUserAsync(TenantContext tenant, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Update the signature counter and last-used timestamp after a successful assertion.</summary>
    Task UpdateAfterUseAsync(TenantContext tenant, Guid credentialId, uint signatureCounter, DateTimeOffset lastUsedAt, CancellationToken cancellationToken = default);

    /// <summary>Remove (revoke) a credential.</summary>
    Task DeleteAsync(TenantContext tenant, Guid credentialId, CancellationToken cancellationToken = default);
}

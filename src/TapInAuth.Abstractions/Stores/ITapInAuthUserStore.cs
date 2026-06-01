namespace TapInAuth.Stores;

/// <summary>
/// User lookup and creation. Tenant-aware: every call receives a <see cref="TenantContext"/>.
/// Implementations must enforce tenant isolation — never return a user from another tenant.
/// </summary>
public interface ITapInAuthUserStore
{
    /// <summary>Find a user by email within the tenant. Returns null if not found.</summary>
    Task<TapInAuthUser?> FindByEmailAsync(TenantContext tenant, string email, CancellationToken cancellationToken = default);

    /// <summary>Find a user by ID within the tenant. Returns null if not found or belongs to another tenant.</summary>
    Task<TapInAuthUser?> FindByIdAsync(TenantContext tenant, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Create a new user in the tenant with the given email. Caller has already validated the email format.</summary>
    Task<TapInAuthUser> CreateAsync(TenantContext tenant, string email, CancellationToken cancellationToken = default);

    /// <summary>Mark the user's email as verified. Idempotent.</summary>
    Task SetEmailVerifiedAsync(TenantContext tenant, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find a user by E.164 phone within the tenant. Returns null if no user has that phone registered.
    /// Phone is a secondary identifier — a user must already exist (registered via email) and have their
    /// phone set via <see cref="SetPhoneAsync"/> before they can sign in by SMS-OTP.
    /// </summary>
    Task<TapInAuthUser?> FindByPhoneAsync(TenantContext tenant, string phone, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set (or clear, when <paramref name="phone"/> is null) the user's phone number. Clears the
    /// <c>PhoneVerified</c> flag whenever the phone changes — re-verification is required after every change.
    /// </summary>
    Task SetPhoneAsync(TenantContext tenant, Guid userId, string? phone, CancellationToken cancellationToken = default);

    /// <summary>Mark the user's phone as verified. Idempotent.</summary>
    Task SetPhoneVerifiedAsync(TenantContext tenant, Guid userId, CancellationToken cancellationToken = default);
}

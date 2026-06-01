using TapInAuth.Tokens;

namespace TapInAuth.Stores;

/// <summary>
/// Storage for one-time-passcode records. All operations are tenant-scoped.
/// </summary>
public interface IOtpCodeStore
{
    /// <summary>Persist a freshly issued OTP record.</summary>
    Task SaveAsync(OtpCode otp, CancellationToken cancellationToken = default);

    /// <summary>Find the most recent unconsumed OTP for the (tenant, user, channel) tuple. Null if none.</summary>
    Task<OtpCode?> FindActiveAsync(TenantContext tenant, Guid userId, OtpChannel channel, CancellationToken cancellationToken = default);

    /// <summary>Mark an OTP as consumed (single-use). Idempotent.</summary>
    Task MarkConsumedAsync(TenantContext tenant, Guid otpId, DateTimeOffset consumedAt, CancellationToken cancellationToken = default);

    /// <summary>Increment the failed-attempt counter on an OTP. Returns the new count.</summary>
    Task<int> IncrementAttemptAsync(TenantContext tenant, Guid otpId, CancellationToken cancellationToken = default);

    /// <summary>Hard-delete OTPs that expired before <paramref name="cutoff"/>. Returns number deleted.</summary>
    Task<int> PurgeExpiredAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default);
}

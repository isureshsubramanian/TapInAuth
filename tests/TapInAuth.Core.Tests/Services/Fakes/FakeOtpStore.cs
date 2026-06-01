using TapInAuth.Stores;
using TapInAuth.Tokens;

namespace TapInAuth.Core.Tests.Services.Fakes;

/// <summary>In-memory <see cref="IOtpCodeStore"/> — keeps the records in a list so tests can inspect them.</summary>
public sealed class FakeOtpStore : IOtpCodeStore
{
    /// <summary>All OTP records that have been saved (in issuance order).</summary>
    public List<OtpCode> Saved { get; } = new();

    public Task SaveAsync(OtpCode otp, CancellationToken cancellationToken = default)
    {
        Saved.Add(otp);
        return Task.CompletedTask;
    }

    public Task<OtpCode?> FindActiveAsync(TenantContext tenant, Guid userId, OtpChannel channel, CancellationToken cancellationToken = default)
    {
        // "Active" = unconsumed. Pick the most-recent matching record.
        var match = Saved
            .Where(o => o.TenantId == tenant.Id && o.UserId == userId && o.Channel == channel && o.ConsumedAt is null)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault();
        return Task.FromResult<OtpCode?>(match);
    }

    public Task MarkConsumedAsync(TenantContext tenant, Guid otpId, DateTimeOffset consumedAt, CancellationToken cancellationToken = default)
    {
        var otp = Saved.FirstOrDefault(o => o.Id == otpId && o.TenantId == tenant.Id);
        if (otp is not null)
        {
            otp.ConsumedAt = consumedAt;
        }
        return Task.CompletedTask;
    }

    public Task<int> IncrementAttemptAsync(TenantContext tenant, Guid otpId, CancellationToken cancellationToken = default)
    {
        var otp = Saved.FirstOrDefault(o => o.Id == otpId && o.TenantId == tenant.Id);
        if (otp is null)
        {
            return Task.FromResult(0);
        }
        otp.AttemptCount += 1;
        return Task.FromResult(otp.AttemptCount);
    }

    public Task<int> PurgeExpiredAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default)
    {
        var removed = Saved.RemoveAll(o => o.ExpiresAt < cutoff);
        return Task.FromResult(removed);
    }
}

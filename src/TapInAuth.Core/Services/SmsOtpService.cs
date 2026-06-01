using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TapInAuth.Auditing;
using TapInAuth.Core.Security;
using TapInAuth.Delivery;
using TapInAuth.Options;
using TapInAuth.RateLimiting;
using TapInAuth.Stores;
using TapInAuth.Tokens;

namespace TapInAuth.Core.Services;

/// <summary>
/// Issues and verifies SMS one-time-passcodes by phone number.
/// </summary>
/// <remarks>
/// <para>
/// Phone is a <em>secondary</em> identifier in TapInAuth v1.0 — the user must already exist (registered
/// via email) and have their phone set via <c>ITapInAuthUserStore.SetPhoneAsync</c>. There is no
/// phone-only signup path — making Email nullable in <c>TapInAuthUser</c> would cascade across
/// magic-link, the Identity adapter, the claims factory, and every store implementation, so v1.0
/// keeps email as primary identity and phone as a verified alternate login channel.
/// </para>
/// <para>
/// Account-recovery semantics mirror <see cref="EmailOtpService"/>: per-phone rate limit on issuance
/// and verification, max-attempt counter, single-use redemption, constant-time hash compare.
/// </para>
/// </remarks>
public sealed class SmsOtpService
{
    private readonly IOptions<TapInAuthOptions> _options;
    private readonly ITapInAuthUserStore _userStore;
    private readonly IOtpCodeStore _otpStore;
    private readonly ISmsSender _smsSender;
    private readonly TokenHasher _hasher;
    private readonly IRateLimiter _rateLimiter;
    private readonly IAuditSink _audit;
    private readonly TapInAuthClaimsPrincipalFactory _principalFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SmsOtpService> _logger;

    /// <summary>Construct an SMS-OTP service.</summary>
    public SmsOtpService(
        IOptions<TapInAuthOptions> options,
        ITapInAuthUserStore userStore,
        IOtpCodeStore otpStore,
        ISmsSender smsSender,
        TokenHasher hasher,
        IRateLimiter rateLimiter,
        IAuditSink audit,
        TapInAuthClaimsPrincipalFactory principalFactory,
        TimeProvider timeProvider,
        ILogger<SmsOtpService> logger)
    {
        _options = options;
        _userStore = userStore;
        _otpStore = otpStore;
        _smsSender = smsSender;
        _hasher = hasher;
        _rateLimiter = rateLimiter;
        _audit = audit;
        _principalFactory = principalFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Issue an SMS OTP for the given phone number.
    /// Always returns <see cref="SmsOtpIssueResult.Issued"/> for unknown phones — this prevents enumeration
    /// of which phone numbers are registered. Real issuance only happens when a matching user exists.
    /// </summary>
    public async Task<SmsOtpIssueResult> IssueAsync(TenantContext tenant, string phone, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);

        if (!PhoneNumber.TryNormalize(phone, out var normalized))
        {
            // Looks malformed. Still return Issued to avoid leaking whether the format is acceptable —
            // a probing attacker can't distinguish "invalid format" from "valid format, unknown phone".
            await Audit(AuditEventType.OtpInvalid, tenant, null, phone, false, "invalid phone format", cancellationToken).ConfigureAwait(false);
            return SmsOtpIssueResult.Issued;
        }

        var rateKey = $"sms-otp:issue:{tenant.Id}:{normalized}";
        if (!await _rateLimiter.TryAcquireAsync(rateKey, 1, cancellationToken).ConfigureAwait(false))
        {
            await Audit(AuditEventType.RateLimitTriggered, tenant, null, normalized, false, "sms otp issue rate-limited", cancellationToken).ConfigureAwait(false);
            return SmsOtpIssueResult.RateLimited;
        }

        var user = await _userStore.FindByPhoneAsync(tenant, normalized, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            // Unknown phone. Don't reveal that — drop silently. The rate-limit above keeps this cheap.
            _logger.LogInformation("SMS OTP issue for unregistered phone in tenant {Tenant}; silently dropping.", tenant.Id);
            return SmsOtpIssueResult.Issued;
        }

        var opts = _options.Value;
        var now = _timeProvider.GetUtcNow();
        var rawCode = TokenGenerator.GenerateOtp(opts.Security.OtpDigits);
        var code = new OtpCode
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            UserId = user.Id,
            Destination = normalized,
            Channel = OtpChannel.Sms,
            CodeHash = _hasher.Hash(rawCode),
            CreatedAt = now,
            ExpiresAt = now + opts.Security.OtpLifetime,
        };
        await _otpStore.SaveAsync(code, cancellationToken).ConfigureAwait(false);

        var message = BuildSmsMessage(opts, tenant, rawCode, normalized);
        var sent = await _smsSender.SendAsync(message, cancellationToken).ConfigureAwait(false);
        await Audit(AuditEventType.OtpIssued, tenant, user.Id.ToString(), normalized, sent, sent ? null : "sms delivery failed", cancellationToken).ConfigureAwait(false);
        return sent ? SmsOtpIssueResult.Issued : SmsOtpIssueResult.DeliveryFailed;
    }

    /// <summary>Verify an SMS OTP that the user entered. Returns the authentication result.</summary>
    public async Task<AuthenticationResult> VerifyAsync(TenantContext tenant, string phone, string rawCode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawCode);

        if (!PhoneNumber.TryNormalize(phone, out var normalized))
        {
            await Audit(AuditEventType.OtpInvalid, tenant, null, phone, false, "invalid phone format", cancellationToken).ConfigureAwait(false);
            return AuthenticationResult.Failure("invalid", TapInAuthMethod.SmsOtp);
        }
        rawCode = new string(rawCode.Where(char.IsDigit).ToArray());

        var rateKey = $"sms-otp:verify:{tenant.Id}:{normalized}";
        if (!await _rateLimiter.TryAcquireAsync(rateKey, 1, cancellationToken).ConfigureAwait(false))
        {
            await Audit(AuditEventType.RateLimitTriggered, tenant, null, normalized, false, "sms otp verify rate-limited", cancellationToken).ConfigureAwait(false);
            return AuthenticationResult.RateLimited();
        }

        var user = await _userStore.FindByPhoneAsync(tenant, normalized, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            await Audit(AuditEventType.OtpInvalid, tenant, null, normalized, false, "unknown phone", cancellationToken).ConfigureAwait(false);
            return AuthenticationResult.Failure("invalid", TapInAuthMethod.SmsOtp);
        }

        var otp = await _otpStore.FindActiveAsync(tenant, user.Id, OtpChannel.Sms, cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();
        if (otp is null || otp.ExpiresAt <= now)
        {
            await Audit(AuditEventType.OtpInvalid, tenant, user.Id.ToString(), normalized, false, otp is null ? "no active otp" : "expired", cancellationToken).ConfigureAwait(false);
            return AuthenticationResult.Failure("expired", TapInAuthMethod.SmsOtp);
        }

        if (otp.AttemptCount >= _options.Value.Security.MaxOtpAttempts)
        {
            await _otpStore.MarkConsumedAsync(tenant, otp.Id, now, cancellationToken).ConfigureAwait(false);
            await Audit(AuditEventType.OtpAttemptsExceeded, tenant, user.Id.ToString(), normalized, false, null, cancellationToken).ConfigureAwait(false);
            return AuthenticationResult.Failure("attempts_exceeded", TapInAuthMethod.SmsOtp);
        }

        var computed = _hasher.Hash(rawCode);
        if (!TokenHasher.FixedTimeEquals(computed, otp.CodeHash))
        {
            await _otpStore.IncrementAttemptAsync(tenant, otp.Id, cancellationToken).ConfigureAwait(false);
            await Audit(AuditEventType.OtpInvalid, tenant, user.Id.ToString(), normalized, false, "hash mismatch", cancellationToken).ConfigureAwait(false);
            return AuthenticationResult.Failure("invalid", TapInAuthMethod.SmsOtp);
        }

        await _otpStore.MarkConsumedAsync(tenant, otp.Id, now, cancellationToken).ConfigureAwait(false);
        if (!user.PhoneVerified)
        {
            await _userStore.SetPhoneVerifiedAsync(tenant, user.Id, cancellationToken).ConfigureAwait(false);
            user = user with { PhoneVerified = true };
        }

        var principal = _principalFactory.Create(user, tenant, TapInAuthMethod.SmsOtp, now);
        await Audit(AuditEventType.OtpVerified, tenant, user.Id.ToString(), normalized, true, null, cancellationToken).ConfigureAwait(false);
        return AuthenticationResult.Success(principal, user, TapInAuthMethod.SmsOtp);
    }

    private static SmsMessage BuildSmsMessage(TapInAuthOptions opts, TenantContext tenant, string code, string phone)
    {
        var tenantName = tenant.DisplayName ?? opts.FromDisplayName ?? "TapInAuth";
        // SMS body is intentionally short — most carriers truncate above ~160 chars and security-code
        // SMSes traditionally read "<code> is your <app> code". Don't include returnUrls or links.
        var body = $"{code} is your {tenantName} sign-in code. Expires in {(int)opts.Security.OtpLifetime.TotalMinutes} min. Reply STOP to opt out.";
        return new SmsMessage(To: phone, Body: body);
    }

    private Task Audit(AuditEventType type, TenantContext tenant, string? userId, string? phone, bool success, string? detail, CancellationToken ct)
        // Reuse the Email column of AuditEvent for the phone destination — the schema doesn't have a
        // dedicated Phone column and the SIEM consumers we've talked to treat it as "destination".
        => _audit.WriteAsync(new AuditEvent(_timeProvider.GetUtcNow(), tenant.Id, type, userId, phone, null, null, detail, success), ct);
}

/// <summary>The outcome of issuing an SMS OTP.</summary>
public enum SmsOtpIssueResult
{
    /// <summary>OTP was issued and delivery was accepted by the provider — or the phone was unknown and we silently dropped (don't leak).</summary>
    Issued,
    /// <summary>Issuance was rate-limited.</summary>
    RateLimited,
    /// <summary>Provider rejected the delivery.</summary>
    DeliveryFailed,
}

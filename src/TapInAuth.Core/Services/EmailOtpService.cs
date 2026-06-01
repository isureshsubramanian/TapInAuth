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

/// <summary>Issues and verifies email one-time-passcodes.</summary>
public sealed class EmailOtpService
{
    private readonly IOptions<TapInAuthOptions> _options;
    private readonly ITapInAuthUserStore _userStore;
    private readonly IOtpCodeStore _otpStore;
    private readonly IEmailSender _emailSender;
    private readonly TokenHasher _hasher;
    private readonly IRateLimiter _rateLimiter;
    private readonly IAuditSink _audit;
    private readonly TapInAuthClaimsPrincipalFactory _principalFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EmailOtpService> _logger;

    /// <summary>Construct an email-OTP service.</summary>
    public EmailOtpService(
        IOptions<TapInAuthOptions> options,
        ITapInAuthUserStore userStore,
        IOtpCodeStore otpStore,
        IEmailSender emailSender,
        TokenHasher hasher,
        IRateLimiter rateLimiter,
        IAuditSink audit,
        TapInAuthClaimsPrincipalFactory principalFactory,
        TimeProvider timeProvider,
        ILogger<EmailOtpService> logger)
    {
        _options = options;
        _userStore = userStore;
        _otpStore = otpStore;
        _emailSender = emailSender;
        _hasher = hasher;
        _rateLimiter = rateLimiter;
        _audit = audit;
        _principalFactory = principalFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>Issue an email OTP for the given email.</summary>
    public async Task<OtpIssueResult> IssueAsync(TenantContext tenant, string email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        email = email.Trim().ToLowerInvariant();
        var rateKey = $"otp:issue:{tenant.Id}:{email}";
        if (!await _rateLimiter.TryAcquireAsync(rateKey, 1, cancellationToken).ConfigureAwait(false))
        {
            await Audit(AuditEventType.RateLimitTriggered, tenant, null, email, false, "otp issue rate-limited", cancellationToken).ConfigureAwait(false);
            return OtpIssueResult.RateLimited;
        }

        var opts = _options.Value;
        var user = await _userStore.FindByEmailAsync(tenant, email, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            if (!opts.Security.AllowSignUp)
            {
                _logger.LogInformation("OTP issue for unknown email in tenant {Tenant}; AllowSignUp=false, silently dropping.", tenant.Id);
                return OtpIssueResult.Issued;
            }
            user = await _userStore.CreateAsync(tenant, email, cancellationToken).ConfigureAwait(false);
            await Audit(AuditEventType.UserCreated, tenant, user.Id.ToString(), email, true, "self-service sign-up via OTP", cancellationToken).ConfigureAwait(false);
        }

        var now = _timeProvider.GetUtcNow();
        var rawCode = TokenGenerator.GenerateOtp(opts.Security.OtpDigits);
        var code = new OtpCode
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            UserId = user.Id,
            Destination = email,
            Channel = OtpChannel.Email,
            CodeHash = _hasher.Hash(rawCode),
            CreatedAt = now,
            ExpiresAt = now + opts.Security.OtpLifetime,
        };
        await _otpStore.SaveAsync(code, cancellationToken).ConfigureAwait(false);

        var message = BuildEmailMessage(opts, tenant, email, rawCode);
        var sent = await _emailSender.SendAsync(message, cancellationToken).ConfigureAwait(false);
        await Audit(AuditEventType.OtpIssued, tenant, user.Id.ToString(), email, sent, sent ? null : "delivery failed", cancellationToken).ConfigureAwait(false);
        return sent ? OtpIssueResult.Issued : OtpIssueResult.DeliveryFailed;
    }

    /// <summary>Verify an OTP that the user entered. Returns the authentication result.</summary>
    public async Task<AuthenticationResult> VerifyAsync(TenantContext tenant, string email, string rawCode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawCode);

        email = email.Trim().ToLowerInvariant();
        rawCode = new string(rawCode.Where(char.IsDigit).ToArray());

        var rateKey = $"otp:verify:{tenant.Id}:{email}";
        if (!await _rateLimiter.TryAcquireAsync(rateKey, 1, cancellationToken).ConfigureAwait(false))
        {
            await Audit(AuditEventType.RateLimitTriggered, tenant, null, email, false, "otp verify rate-limited", cancellationToken).ConfigureAwait(false);
            return AuthenticationResult.RateLimited();
        }

        var user = await _userStore.FindByEmailAsync(tenant, email, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            await Audit(AuditEventType.OtpInvalid, tenant, null, email, false, "unknown email", cancellationToken).ConfigureAwait(false);
            return AuthenticationResult.Failure("invalid", TapInAuthMethod.EmailOtp);
        }

        var otp = await _otpStore.FindActiveAsync(tenant, user.Id, OtpChannel.Email, cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();
        if (otp is null || otp.ExpiresAt <= now)
        {
            await Audit(AuditEventType.OtpInvalid, tenant, user.Id.ToString(), email, false, otp is null ? "no active otp" : "expired", cancellationToken).ConfigureAwait(false);
            return AuthenticationResult.Failure("expired", TapInAuthMethod.EmailOtp);
        }

        if (otp.AttemptCount >= _options.Value.Security.MaxOtpAttempts)
        {
            await _otpStore.MarkConsumedAsync(tenant, otp.Id, now, cancellationToken).ConfigureAwait(false);
            await Audit(AuditEventType.OtpAttemptsExceeded, tenant, user.Id.ToString(), email, false, null, cancellationToken).ConfigureAwait(false);
            return AuthenticationResult.Failure("attempts_exceeded", TapInAuthMethod.EmailOtp);
        }

        var computed = _hasher.Hash(rawCode);
        if (!TokenHasher.FixedTimeEquals(computed, otp.CodeHash))
        {
            await _otpStore.IncrementAttemptAsync(tenant, otp.Id, cancellationToken).ConfigureAwait(false);
            await Audit(AuditEventType.OtpInvalid, tenant, user.Id.ToString(), email, false, "hash mismatch", cancellationToken).ConfigureAwait(false);
            return AuthenticationResult.Failure("invalid", TapInAuthMethod.EmailOtp);
        }

        await _otpStore.MarkConsumedAsync(tenant, otp.Id, now, cancellationToken).ConfigureAwait(false);
        if (!user.EmailVerified)
        {
            await _userStore.SetEmailVerifiedAsync(tenant, user.Id, cancellationToken).ConfigureAwait(false);
            user = user with { EmailVerified = true };
        }

        var principal = _principalFactory.Create(user, tenant, TapInAuthMethod.EmailOtp, now);
        await Audit(AuditEventType.OtpVerified, tenant, user.Id.ToString(), email, true, null, cancellationToken).ConfigureAwait(false);
        return AuthenticationResult.Success(principal, user, TapInAuthMethod.EmailOtp);
    }

    private static EmailMessage BuildEmailMessage(TapInAuthOptions opts, TenantContext tenant, string email, string code)
    {
        var tenantName = tenant.DisplayName ?? opts.FromDisplayName ?? "TapInAuth";
        var html = $"""
            <!doctype html>
            <html><body style="font-family: -apple-system, Segoe UI, sans-serif; background:#F9FAFB; padding:32px;">
              <div style="max-width:480px; margin:0 auto; background:#fff; border-radius:18px; padding:32px; box-shadow:0 4px 24px rgba(0,0,0,0.06);">
                <h2 style="margin:0 0 16px; color:#111827;">Your sign-in code</h2>
                <p style="color:#374151;">Enter this code to finish signing in to {System.Net.WebUtility.HtmlEncode(tenantName)}. It expires in {(int)opts.Security.OtpLifetime.TotalMinutes} minutes.</p>
                <p style="font-size:32px; letter-spacing:8px; font-weight:700; color:#111827; text-align:center; padding:16px; background:#F3F4F6; border-radius:14px;">{code}</p>
                <p style="color:#6B7280; font-size:13px;">If you didn't request this, you can ignore it.</p>
              </div>
            </body></html>
            """;
        var text = $"Your {tenantName} sign-in code: {code}\nExpires in {(int)opts.Security.OtpLifetime.TotalMinutes} minutes.";
        return new EmailMessage(
            To: email,
            Subject: $"{code} is your sign-in code",
            HtmlBody: html,
            PlainTextBody: text,
            From: opts.FromEmail,
            FromDisplayName: opts.FromDisplayName ?? tenantName);
    }

    private Task Audit(AuditEventType type, TenantContext tenant, string? userId, string? email, bool success, string? detail, CancellationToken ct)
        => _audit.WriteAsync(new AuditEvent(_timeProvider.GetUtcNow(), tenant.Id, type, userId, email, null, null, detail, success), ct);
}

/// <summary>The outcome of issuing an OTP.</summary>
public enum OtpIssueResult
{
    Issued,
    RateLimited,
    DeliveryFailed,
}

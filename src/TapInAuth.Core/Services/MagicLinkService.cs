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
/// Issues and redeems magic-link tokens. Stateless beyond its injected dependencies; safe for singleton lifetime.
/// </summary>
public sealed class MagicLinkService
{
    private readonly IOptions<TapInAuthOptions> _options;
    private readonly ITapInAuthUserStore _userStore;
    private readonly IMagicLinkTokenStore _tokenStore;
    private readonly IEmailSender _emailSender;
    private readonly TokenHasher _hasher;
    private readonly IRateLimiter _rateLimiter;
    private readonly IAuditSink _audit;
    private readonly TapInAuthClaimsPrincipalFactory _principalFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MagicLinkService> _logger;

    /// <summary>Construct a magic-link service.</summary>
    public MagicLinkService(
        IOptions<TapInAuthOptions> options,
        ITapInAuthUserStore userStore,
        IMagicLinkTokenStore tokenStore,
        IEmailSender emailSender,
        TokenHasher hasher,
        IRateLimiter rateLimiter,
        IAuditSink audit,
        TapInAuthClaimsPrincipalFactory principalFactory,
        TimeProvider timeProvider,
        ILogger<MagicLinkService> logger)
    {
        _options = options;
        _userStore = userStore;
        _tokenStore = tokenStore;
        _emailSender = emailSender;
        _hasher = hasher;
        _rateLimiter = rateLimiter;
        _audit = audit;
        _principalFactory = principalFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Issue a magic link for the given email and send it. If the email is unknown and self-service
    /// sign-up is enabled, a new user is created.
    /// </summary>
    public async Task<MagicLinkIssueResult> IssueAsync(
        TenantContext tenant,
        string email,
        string magicLinkUrlTemplate,
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(magicLinkUrlTemplate);

        email = email.Trim().ToLowerInvariant();

        var rateKey = $"magiclink:issue:{tenant.Id}:{email}";
        if (!await _rateLimiter.TryAcquireAsync(rateKey, 1, cancellationToken).ConfigureAwait(false))
        {
            await EmitAuditAsync(tenant, AuditEventType.RateLimitTriggered, null, email, false, "magic-link issue rate-limited", cancellationToken).ConfigureAwait(false);
            return MagicLinkIssueResult.RateLimited;
        }

        var opts = _options.Value;

        var user = await _userStore.FindByEmailAsync(tenant, email, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            if (!opts.Security.AllowSignUp)
            {
                // Don't reveal that the account doesn't exist — return success-shaped result without sending.
                _logger.LogInformation("Magic-link issue for unknown email in tenant {Tenant}; AllowSignUp=false, silently dropping.", tenant.Id);
                return MagicLinkIssueResult.Issued;
            }
            user = await _userStore.CreateAsync(tenant, email, cancellationToken).ConfigureAwait(false);
            await EmitAuditAsync(tenant, AuditEventType.UserCreated, user.Id.ToString(), email, true, "self-service sign-up via magic link", cancellationToken).ConfigureAwait(false);
        }

        var now = _timeProvider.GetUtcNow();
        var tokenId = Guid.NewGuid();
        var rawToken = TokenGenerator.GenerateMagicLinkToken();
        var tokenHash = _hasher.Hash(rawToken);

        var record = new MagicLinkToken
        {
            Id = tokenId,
            TenantId = tenant.Id,
            UserId = user.Id,
            Email = email,
            TokenHash = tokenHash,
            CreatedAt = now,
            ExpiresAt = now + opts.Security.MagicLinkLifetime,
            ReturnUrl = returnUrl,
        };
        await _tokenStore.SaveAsync(record, cancellationToken).ConfigureAwait(false);

        var url = magicLinkUrlTemplate
            .Replace("{tokenId}", tokenId.ToString("D"), StringComparison.Ordinal)
            .Replace("{token}", rawToken, StringComparison.Ordinal);

        var message = BuildEmailMessage(opts, tenant, email, url);
        var sent = await _emailSender.SendAsync(message, cancellationToken).ConfigureAwait(false);

        await EmitAuditAsync(tenant, AuditEventType.MagicLinkIssued, user.Id.ToString(), email, sent, sent ? null : "email send failed", cancellationToken).ConfigureAwait(false);

        return sent ? MagicLinkIssueResult.Issued : MagicLinkIssueResult.DeliveryFailed;
    }

    /// <summary>
    /// Redeem a magic-link token. Validates the token, marks it consumed, and returns an authenticated principal.
    /// </summary>
    public async Task<AuthenticationResult> RedeemAsync(
        TenantContext tenant,
        Guid tokenId,
        string rawToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);

        var record = await _tokenStore.FindByIdAsync(tenant, tokenId, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            await EmitAuditAsync(tenant, AuditEventType.MagicLinkInvalid, null, null, false, "token not found", cancellationToken).ConfigureAwait(false);
            return AuthenticationResult.Failure("invalid", TapInAuthMethod.MagicLink);
        }

        var now = _timeProvider.GetUtcNow();
        if (record.ConsumedAt is not null)
        {
            await EmitAuditAsync(tenant, AuditEventType.MagicLinkInvalid, record.UserId.ToString(), record.Email, false, "token already consumed", cancellationToken).ConfigureAwait(false);
            return AuthenticationResult.Failure("consumed", TapInAuthMethod.MagicLink);
        }
        if (record.ExpiresAt <= now)
        {
            await EmitAuditAsync(tenant, AuditEventType.MagicLinkExpired, record.UserId.ToString(), record.Email, false, "token expired", cancellationToken).ConfigureAwait(false);
            return AuthenticationResult.Failure("expired", TapInAuthMethod.MagicLink);
        }

        var computed = _hasher.Hash(rawToken);
        if (!TokenHasher.FixedTimeEquals(computed, record.TokenHash))
        {
            await EmitAuditAsync(tenant, AuditEventType.MagicLinkInvalid, record.UserId.ToString(), record.Email, false, "hash mismatch", cancellationToken).ConfigureAwait(false);
            return AuthenticationResult.Failure("invalid", TapInAuthMethod.MagicLink);
        }

        await _tokenStore.MarkConsumedAsync(tenant, record.Id, now, cancellationToken).ConfigureAwait(false);

        var user = await _userStore.FindByIdAsync(tenant, record.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            await EmitAuditAsync(tenant, AuditEventType.MagicLinkInvalid, record.UserId.ToString(), record.Email, false, "user missing after redeem", cancellationToken).ConfigureAwait(false);
            return AuthenticationResult.Failure("user_missing", TapInAuthMethod.MagicLink);
        }

        if (!user.EmailVerified)
        {
            await _userStore.SetEmailVerifiedAsync(tenant, user.Id, cancellationToken).ConfigureAwait(false);
            user = user with { EmailVerified = true };
        }

        var principal = _principalFactory.Create(user, tenant, TapInAuthMethod.MagicLink, now);
        await EmitAuditAsync(tenant, AuditEventType.MagicLinkRedeemed, user.Id.ToString(), user.Email, true, null, cancellationToken).ConfigureAwait(false);
        return AuthenticationResult.Success(principal, user, TapInAuthMethod.MagicLink, record.ReturnUrl);
    }

    private static EmailMessage BuildEmailMessage(TapInAuthOptions opts, TenantContext tenant, string email, string url)
    {
        var tenantName = tenant.DisplayName ?? opts.FromDisplayName ?? "TapInAuth";
        var html = $"""
            <!doctype html>
            <html><body style="font-family: -apple-system, Segoe UI, sans-serif; background:#F9FAFB; padding:32px;">
              <div style="max-width:480px; margin:0 auto; background:#fff; border-radius:18px; padding:32px; box-shadow:0 4px 24px rgba(0,0,0,0.06);">
                <h2 style="margin:0 0 16px; color:#111827;">Sign in to {System.Net.WebUtility.HtmlEncode(tenantName)}</h2>
                <p style="color:#374151; line-height:1.5;">Click the button below to finish signing in. This link expires in {(int)opts.Security.MagicLinkLifetime.TotalMinutes} minutes and can only be used once.</p>
                <p style="margin:24px 0;">
                  <a href="{System.Net.WebUtility.HtmlEncode(url)}" target="_blank" rel="noopener noreferrer" style="display:inline-block; background:{opts.Theme.Accent}; color:#fff; text-decoration:none; padding:12px 24px; border-radius:14px; font-weight:600;">Sign in</a>
                </p>
                <p style="color:#6B7280; font-size:13px;">If you didn't request this email, you can ignore it.</p>
              </div>
            </body></html>
            """;
        var text = $"Sign in to {tenantName}: {url}\nThis link expires in {(int)opts.Security.MagicLinkLifetime.TotalMinutes} minutes.";
        return new EmailMessage(
            To: email,
            Subject: $"Sign in to {tenantName}",
            HtmlBody: html,
            PlainTextBody: text,
            From: opts.FromEmail,
            FromDisplayName: opts.FromDisplayName ?? tenantName);
    }

    private Task EmitAuditAsync(TenantContext tenant, AuditEventType type, string? userId, string? email, bool success, string? detail, CancellationToken ct)
        => _audit.WriteAsync(new AuditEvent(
            Timestamp: _timeProvider.GetUtcNow(),
            TenantId: tenant.Id,
            Type: type,
            UserId: userId,
            Email: email,
            IpAddress: null,
            UserAgent: null,
            Detail: detail,
            Success: success), ct);
}

/// <summary>The outcome of issuing a magic link.</summary>
public enum MagicLinkIssueResult
{
    /// <summary>The link was issued and the email accepted by the provider.</summary>
    Issued,
    /// <summary>The request was rate-limited; no email was sent.</summary>
    RateLimited,
    /// <summary>The email provider rejected the message.</summary>
    DeliveryFailed,
}

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TapInAuth.Auditing;
using TapInAuth.Core.Security;
using TapInAuth.Options;
using TapInAuth.RateLimiting;
using TapInAuth.Stores;
using TapInAuth.Tokens;

namespace TapInAuth.Core.Services;

/// <summary>
/// Generates and redeems one-time recovery codes — the rescue path when a user has lost their
/// passkey / device. Codes are presented to the user once at generation; only HMAC-SHA256 hashes
/// are stored. Redemption is rate-limited and constant-time.
/// </summary>
public sealed class RecoveryCodeService
{
    private readonly IOptions<TapInAuthOptions> _options;
    private readonly ITapInAuthUserStore _userStore;
    private readonly IRecoveryCodeStore _recoveryStore;
    private readonly TokenHasher _hasher;
    private readonly IRateLimiter _rateLimiter;
    private readonly IAuditSink _audit;
    private readonly TapInAuthClaimsPrincipalFactory _principalFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RecoveryCodeService> _logger;

    /// <summary>Construct the service.</summary>
    public RecoveryCodeService(
        IOptions<TapInAuthOptions> options,
        ITapInAuthUserStore userStore,
        IRecoveryCodeStore recoveryStore,
        TokenHasher hasher,
        IRateLimiter rateLimiter,
        IAuditSink audit,
        TapInAuthClaimsPrincipalFactory principalFactory,
        TimeProvider timeProvider,
        ILogger<RecoveryCodeService> logger)
    {
        _options = options;
        _userStore = userStore;
        _recoveryStore = recoveryStore;
        _hasher = hasher;
        _rateLimiter = rateLimiter;
        _audit = audit;
        _principalFactory = principalFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Wipe all of a user's previous recovery codes and generate a fresh batch. Returns the
    /// plaintext codes — show them to the user ONCE; they cannot be retrieved later.
    /// </summary>
    public async Task<IReadOnlyList<string>> RegenerateAsync(TenantContext tenant, Guid userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        var opts = _options.Value.Security;
        var count = Math.Clamp(opts.RecoveryCodeCount, 4, 20);
        var length = Math.Clamp(opts.RecoveryCodeLength, 8, 20);

        await _recoveryStore.DeleteAllForUserAsync(tenant, userId, cancellationToken).ConfigureAwait(false);

        var now = _timeProvider.GetUtcNow();
        var plain = new List<string>(count);
        var rows = new List<RecoveryCode>(count);
        for (var i = 0; i < count; i++)
        {
            var code = GenerateCode(length);
            plain.Add(code);
            rows.Add(new RecoveryCode
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                UserId = userId,
                CodeHash = _hasher.Hash(Normalize(code)),
                CreatedAt = now,
            });
        }
        await _recoveryStore.SaveBatchAsync(rows, cancellationToken).ConfigureAwait(false);

        await _audit.WriteAsync(new AuditEvent(
            now, tenant.Id, AuditEventType.CredentialRegistered,
            userId.ToString(), null, null, null,
            $"recovery codes regenerated ({count})", true),
            cancellationToken).ConfigureAwait(false);

        return plain;
    }

    /// <summary>Count the unconsumed codes a user has left.</summary>
    public Task<int> CountRemainingAsync(TenantContext tenant, Guid userId, CancellationToken cancellationToken = default)
        => _recoveryStore.CountActiveAsync(tenant, userId, cancellationToken);

    /// <summary>
    /// Redeem a recovery code for the given email. Returns the matching user on success or null on failure.
    /// Caller is responsible for issuing the sign-in (e.g., handing the principal to the host's cookie scheme).
    /// </summary>
    public async Task<TapInAuthUser?> RedeemAsync(TenantContext tenant, string email, string code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        email = email.Trim().ToLowerInvariant();
        var rateKey = $"recovery:verify:{tenant.Id}:{email}";
        if (!await _rateLimiter.TryAcquireAsync(rateKey, 1, cancellationToken).ConfigureAwait(false))
        {
            await Audit(AuditEventType.RateLimitTriggered, tenant, null, email, false, "recovery rate-limited", cancellationToken).ConfigureAwait(false);
            return null;
        }

        var user = await _userStore.FindByEmailAsync(tenant, email, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            await Audit(AuditEventType.OtpInvalid, tenant, null, email, false, "recovery: unknown email", cancellationToken).ConfigureAwait(false);
            return null;
        }

        var attempt = _hasher.Hash(Normalize(code));
        var active = await _recoveryStore.ListActiveAsync(tenant, user.Id, cancellationToken).ConfigureAwait(false);
        var match = active.FirstOrDefault(c => TokenHasher.FixedTimeEquals(c.CodeHash, attempt));
        if (match is null)
        {
            await Audit(AuditEventType.OtpInvalid, tenant, user.Id.ToString(), email, false, "recovery: no matching code", cancellationToken).ConfigureAwait(false);
            return null;
        }

        await _recoveryStore.MarkConsumedAsync(tenant, match.Id, _timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        await Audit(AuditEventType.OtpVerified, tenant, user.Id.ToString(), email, true, "recovery code redeemed", cancellationToken).ConfigureAwait(false);
        return user;
    }

    /// <summary>Build a claims principal for a user who just authenticated via recovery code.</summary>
    public System.Security.Claims.ClaimsPrincipal BuildPrincipal(TenantContext tenant, TapInAuthUser user)
        => _principalFactory.Create(user, tenant, TapInAuthMethod.RecoveryCode, _timeProvider.GetUtcNow());

    // ──────────────────────── helpers ────────────────────────

    private static string GenerateCode(int length)
    {
        // Crockford-style alphabet — no 0/O/1/I to reduce read-aloud confusion.
        const string alphabet = "23456789ABCDEFGHJKMNPQRSTVWXYZ";
        Span<char> chars = stackalloc char[length];
        Span<byte> buffer = stackalloc byte[1];
        for (var i = 0; i < length; i++)
        {
            byte b;
            do
            {
                RandomNumberGenerator.Fill(buffer);
                b = buffer[0];
            } while (b >= 240); // reject to keep distribution uniform across 30-char alphabet
            chars[i] = alphabet[b % alphabet.Length];
        }
        // Insert a hyphen at the midpoint for readability (e.g., "ABCDE-FGHJK").
        var mid = length / 2;
        return string.Concat(new string(chars[..mid]), "-", new string(chars[mid..]));
    }

    private static string Normalize(string code)
    {
        // Accept user input in any case, with or without the hyphen / spaces.
        var sb = new StringBuilder(code.Length);
        foreach (var c in code)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(char.ToUpperInvariant(c));
            }
        }
        return sb.ToString();
    }

    private Task Audit(AuditEventType type, TenantContext tenant, string? userId, string? email, bool success, string? detail, CancellationToken ct)
        => _audit.WriteAsync(new AuditEvent(_timeProvider.GetUtcNow(), tenant.Id, type, userId, email, null, null, detail, success), ct);
}

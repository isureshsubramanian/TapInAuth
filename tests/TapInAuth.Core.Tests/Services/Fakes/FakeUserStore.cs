using TapInAuth.Stores;

namespace TapInAuth.Core.Tests.Services.Fakes;

/// <summary>
/// In-memory <see cref="ITapInAuthUserStore"/> for unit tests. Keeps the surface tiny — just
/// enough to satisfy MagicLinkService / EmailOtpService / SmsOtpService dependencies and let
/// tests assert about which calls were made.
/// </summary>
public sealed class FakeUserStore : ITapInAuthUserStore
{
    private readonly Dictionary<Guid, TapInAuthUser> _byId = new();

    /// <summary>Track which user ids had SetPhoneVerifiedAsync called.</summary>
    public List<Guid> PhoneVerifiedCalls { get; } = new();

    /// <summary>Track which user ids had SetEmailVerifiedAsync called.</summary>
    public List<Guid> EmailVerifiedCalls { get; } = new();

    /// <summary>Track all CreateAsync (signup) calls — useful for verifying the AllowSignUp gate.</summary>
    public List<string> CreatedEmails { get; } = new();

    public TapInAuthUser Seed(Guid id, string email, bool emailVerified = false)
    {
        var u = new TapInAuthUser(id, TenantContext.DefaultTenantId, email.ToLowerInvariant(), emailVerified, DateTimeOffset.UtcNow);
        _byId[id] = u;
        return u;
    }

    public TapInAuthUser SeedWithPhone(Guid id, string email, string phone, bool phoneVerified = false)
    {
        var u = new TapInAuthUser(id, TenantContext.DefaultTenantId, email.ToLowerInvariant(), false, DateTimeOffset.UtcNow,
            Phone: phone, PhoneVerified: phoneVerified);
        _byId[id] = u;
        return u;
    }

    public Task<TapInAuthUser?> FindByEmailAsync(TenantContext tenant, string email, CancellationToken cancellationToken = default)
    {
        var match = _byId.Values.FirstOrDefault(u => u.TenantId == tenant.Id && string.Equals(u.Email, email.Trim().ToLowerInvariant(), StringComparison.Ordinal));
        return Task.FromResult<TapInAuthUser?>(match);
    }

    public Task<TapInAuthUser?> FindByIdAsync(TenantContext tenant, Guid userId, CancellationToken cancellationToken = default)
    {
        if (_byId.TryGetValue(userId, out var u) && u.TenantId == tenant.Id)
        {
            return Task.FromResult<TapInAuthUser?>(u);
        }
        return Task.FromResult<TapInAuthUser?>(null);
    }

    public Task<TapInAuthUser> CreateAsync(TenantContext tenant, string email, CancellationToken cancellationToken = default)
    {
        var u = new TapInAuthUser(Guid.NewGuid(), tenant.Id, email.Trim().ToLowerInvariant(), false, DateTimeOffset.UtcNow);
        _byId[u.Id] = u;
        CreatedEmails.Add(u.Email);
        return Task.FromResult(u);
    }

    public Task SetEmailVerifiedAsync(TenantContext tenant, Guid userId, CancellationToken cancellationToken = default)
    {
        EmailVerifiedCalls.Add(userId);
        if (_byId.TryGetValue(userId, out var u))
        {
            _byId[userId] = u with { EmailVerified = true };
        }
        return Task.CompletedTask;
    }

    public Task<TapInAuthUser?> FindByPhoneAsync(TenantContext tenant, string phone, CancellationToken cancellationToken = default)
    {
        if (!TapInAuth.PhoneNumber.TryNormalize(phone, out var normalized))
        {
            return Task.FromResult<TapInAuthUser?>(null);
        }
        var match = _byId.Values.FirstOrDefault(u => u.TenantId == tenant.Id && u.Phone == normalized);
        return Task.FromResult<TapInAuthUser?>(match);
    }

    public Task SetPhoneAsync(TenantContext tenant, Guid userId, string? phone, CancellationToken cancellationToken = default)
    {
        if (!_byId.TryGetValue(userId, out var u))
        {
            return Task.CompletedTask;
        }
        string? normalized = null;
        if (!string.IsNullOrEmpty(phone))
        {
            if (!TapInAuth.PhoneNumber.TryNormalize(phone, out var n))
            {
                throw new ArgumentException("invalid phone", nameof(phone));
            }
            normalized = n;
        }
        _byId[userId] = u with { Phone = normalized, PhoneVerified = false };
        return Task.CompletedTask;
    }

    public Task SetPhoneVerifiedAsync(TenantContext tenant, Guid userId, CancellationToken cancellationToken = default)
    {
        PhoneVerifiedCalls.Add(userId);
        if (_byId.TryGetValue(userId, out var u) && u.Phone is not null)
        {
            _byId[userId] = u with { PhoneVerified = true };
        }
        return Task.CompletedTask;
    }
}

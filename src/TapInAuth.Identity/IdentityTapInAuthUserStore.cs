using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using TapInAuth.Stores;

namespace TapInAuth.Identity;

/// <summary>
/// <see cref="ITapInAuthUserStore"/> implementation that delegates to ASP.NET Core Identity's
/// <see cref="UserManager{TUser}"/>. Use this when the host app already has an Identity user table
/// and wants TapInAuth to operate on that same table instead of TapInAuth's own EF Core user table.
/// </summary>
/// <remarks>
/// <para>
/// Assumes <typeparamref name="TUser"/>'s primary key is a <see cref="Guid"/>-formatted string —
/// the default for <see cref="IdentityUser"/>. If your project uses an int or custom key, write
/// a specialized store instead.
/// </para>
/// <para>
/// Tenant handling is single-tenant by default — every call uses <see cref="TenantContext.Default"/>.
/// Multi-tenant Identity requires a per-tenant <c>DbContext</c> or a tenant-aware <c>IdentityUser</c>
/// subclass; that's out of scope for the default adapter.
/// </para>
/// </remarks>
public sealed class IdentityTapInAuthUserStore<TUser> : ITapInAuthUserStore where TUser : IdentityUser, new()
{
    private readonly UserManager<TUser> _userManager;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<IdentityTapInAuthUserStore<TUser>> _logger;

    /// <summary>Construct the adapter.</summary>
    public IdentityTapInAuthUserStore(
        UserManager<TUser> userManager,
        TimeProvider timeProvider,
        ILogger<IdentityTapInAuthUserStore<TUser>> logger)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<TapInAuthUser?> FindByEmailAsync(TenantContext tenant, string email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var user = await _userManager.FindByEmailAsync(email.Trim()).ConfigureAwait(false);
        return user is null ? null : Map(user);
    }

    /// <inheritdoc />
    public async Task<TapInAuthUser?> FindByIdAsync(TenantContext tenant, Guid userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        var user = await _userManager.FindByIdAsync(userId.ToString("D", CultureInfo.InvariantCulture)).ConfigureAwait(false);
        return user is null ? null : Map(user);
    }

    /// <inheritdoc />
    public async Task<TapInAuthUser> CreateAsync(TenantContext tenant, string email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        email = email.Trim();
        var user = new TUser
        {
            // Identity uses a string primary key by default; use a Guid string so we can round-trip to TapInAuthUser.Id.
            Id = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture),
            Email = email,
            UserName = email,
            EmailConfirmed = false,
        };

        var result = await _userManager.CreateAsync(user).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
            _logger.LogWarning("Identity adapter: CreateAsync failed for {Email}: {Errors}", email, errors);
            throw new InvalidOperationException($"TapInAuth.Identity: failed to create user — {errors}");
        }
        return Map(user);
    }

    /// <inheritdoc />
    public async Task SetEmailVerifiedAsync(TenantContext tenant, Guid userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        var user = await _userManager.FindByIdAsync(userId.ToString("D", CultureInfo.InvariantCulture)).ConfigureAwait(false);
        if (user is null || user.EmailConfirmed)
        {
            return;
        }
        // Generate then redeem an email-confirmation token so Identity's normal pipeline applies (stamp + audit).
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user).ConfigureAwait(false);
        var confirm = await _userManager.ConfirmEmailAsync(user, token).ConfigureAwait(false);
        if (!confirm.Succeeded)
        {
            var errors = string.Join("; ", confirm.Errors.Select(e => $"{e.Code}: {e.Description}"));
            _logger.LogWarning("Identity adapter: ConfirmEmailAsync failed for {UserId}: {Errors}", userId, errors);
        }
    }

    /// <inheritdoc />
    public async Task<TapInAuthUser?> FindByPhoneAsync(TenantContext tenant, string phone, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        if (!PhoneNumber.TryNormalize(phone, out var normalized))
        {
            return null;
        }
        // Identity doesn't expose a FindByPhone API. The Users IQueryable does — most production Identity
        // stores back it with EF Core. If the host has overridden Users to be non-queryable, swap to a
        // custom store.
        var user = _userManager.Users.FirstOrDefault(u => u.PhoneNumber == normalized);
        return user is null ? null : Map(user);
    }

    /// <inheritdoc />
    public async Task SetPhoneAsync(TenantContext tenant, Guid userId, string? phone, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        var user = await _userManager.FindByIdAsync(userId.ToString("D", CultureInfo.InvariantCulture)).ConfigureAwait(false);
        if (user is null)
        {
            return;
        }

        string? normalized = null;
        if (!string.IsNullOrWhiteSpace(phone))
        {
            if (!PhoneNumber.TryNormalize(phone, out var n))
            {
                throw new ArgumentException("Phone number is not a valid E.164 number.", nameof(phone));
            }
            normalized = n;
        }

        if (user.PhoneNumber == normalized)
        {
            return;
        }

        // Use the UserManager API so the security stamp is rotated. SetPhoneNumberAsync also clears
        // PhoneNumberConfirmed under the hood, which matches our "any change clears verified" contract.
        var result = await _userManager.SetPhoneNumberAsync(user, normalized).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
            _logger.LogWarning("Identity adapter: SetPhoneNumberAsync failed for {UserId}: {Errors}", userId, errors);
            throw new InvalidOperationException($"TapInAuth.Identity: failed to set phone — {errors}");
        }
    }

    /// <inheritdoc />
    public async Task SetPhoneVerifiedAsync(TenantContext tenant, Guid userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        var user = await _userManager.FindByIdAsync(userId.ToString("D", CultureInfo.InvariantCulture)).ConfigureAwait(false);
        if (user is null || user.PhoneNumberConfirmed || string.IsNullOrEmpty(user.PhoneNumber))
        {
            return;
        }
        // Generate then redeem a change-phone-number token so Identity rotates the security stamp.
        var token = await _userManager.GenerateChangePhoneNumberTokenAsync(user, user.PhoneNumber).ConfigureAwait(false);
        var result = await _userManager.ChangePhoneNumberAsync(user, user.PhoneNumber, token).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
            _logger.LogWarning("Identity adapter: ChangePhoneNumberAsync failed for {UserId}: {Errors}", userId, errors);
        }
    }

    private static TapInAuthUser Map(TUser user)
    {
        var id = Guid.TryParse(user.Id, out var parsed)
            ? parsed
            : throw new InvalidOperationException(
                $"TapInAuth.Identity: IdentityUser.Id '{user.Id}' is not a Guid. " +
                "The default adapter requires Guid-string primary keys; use a custom ITapInAuthUserStore for other key types.");
        return new TapInAuthUser(
            Id: id,
            TenantId: TenantContext.DefaultTenantId,
            Email: user.Email ?? string.Empty,
            EmailVerified: user.EmailConfirmed,
            CreatedAt: DateTimeOffset.UtcNow,    // Identity doesn't expose CreatedAt; we'd need a custom user type to surface it.
            DisplayName: user.UserName,
            Phone: user.PhoneNumber,
            PhoneVerified: user.PhoneNumberConfirmed);
    }
}

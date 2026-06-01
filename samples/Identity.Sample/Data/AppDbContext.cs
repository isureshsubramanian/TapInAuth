using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TapInAuth.Store.EntityFrameworkCore;

namespace TapInAuth.Samples.Identity.Data;

/// <summary>
/// IdentityDbContext that hosts both Identity's user table AND TapInAuth's auxiliary tables
/// (magic-link tokens, OTP codes, passkey credentials). Identity owns the user table; TapInAuth
/// references users by their Guid-string Id via the adapter.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Adds the TapInAuth tables (TapInAuthMagicLinkTokens, TapInAuthOtpCodes, TapInAuthCredentials)
        // alongside the Identity ones. The TapInAuthUsers table is also registered but unused — the
        // Identity adapter routes user calls to AspNetUsers instead. Keep it for schema completeness;
        // it adds no overhead.
        builder.ApplyTapInAuthConfiguration();
    }
}

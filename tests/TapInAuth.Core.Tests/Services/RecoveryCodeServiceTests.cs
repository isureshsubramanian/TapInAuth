using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using TapInAuth.Auditing;
using TapInAuth.Core.Security;
using TapInAuth.Core.Services;
using TapInAuth.Core.Tests.Services.Fakes;
using TapInAuth.Options;
using Xunit;

namespace TapInAuth.Core.Tests.Services;

public class RecoveryCodeServiceTests
{
    private const string KnownEmail = "alice@acme.test";
    private static readonly Guid KnownUserId = Guid.Parse("99999999-8888-7777-6666-555555555555");

    private static (RecoveryCodeService svc, FakeUserStore users, FakeRecoveryCodeStore codes, FakeRateLimiter rate, FakeAuditSink audit, FakeTimeProvider clock)
        Build(int codeCount = 10, int codeLength = 10, bool rateLimited = false)
    {
        var opts = Microsoft.Extensions.Options.Options.Create(new TapInAuthOptions
        {
            Methods = TapInAuthMethod.RecoveryCode,
            Security =
            {
                RecoveryCodeCount = codeCount,
                RecoveryCodeLength = codeLength,
            },
        });
        var users = new FakeUserStore();
        users.Seed(KnownUserId, KnownEmail);
        var codes = new FakeRecoveryCodeStore();
        var rate = new FakeRateLimiter { AllowAcquire = !rateLimited };
        var audit = new FakeAuditSink();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-01T12:00:00Z"));
        var hasher = new TokenHasher(new byte[32]);
        var principalFactory = new TapInAuthClaimsPrincipalFactory();

        var svc = new RecoveryCodeService(opts, users, codes, hasher, rate, audit, principalFactory, clock, NullLogger<RecoveryCodeService>.Instance);
        return (svc, users, codes, rate, audit, clock);
    }

    // ── Regenerate ───────────────────────────────────────────────────────

    [Fact]
    public async Task Regenerate_produces_configured_count_of_codes_and_audits()
    {
        var (svc, _, codes, _, audit, _) = Build(codeCount: 10);

        var plain = await svc.RegenerateAsync(TenantContext.Default, KnownUserId);

        plain.Should().HaveCount(10);
        codes.Saved.Should().HaveCount(10);
        audit.Events.Should().Contain(e => e.Type == AuditEventType.CredentialRegistered);
    }

    [Fact]
    public async Task Regenerate_clears_existing_codes_first()
    {
        var (svc, _, codes, _, _, _) = Build();
        await svc.RegenerateAsync(TenantContext.Default, KnownUserId);
        codes.Saved.Should().HaveCount(10);

        await svc.RegenerateAsync(TenantContext.Default, KnownUserId);

        codes.Saved.Should().HaveCount(10, "previous batch should have been deleted before saving the new one");
    }

    [Fact]
    public async Task Regenerate_clamps_count_into_the_4_to_20_range()
    {
        var (svc, _, codes, _, _, _) = Build(codeCount: 1);   // below the floor

        var plain = await svc.RegenerateAsync(TenantContext.Default, KnownUserId);

        plain.Should().HaveCount(4, "count is clamped to a minimum of 4");
        codes.Saved.Should().HaveCount(4);
    }

    [Fact]
    public async Task Regenerate_returns_codes_with_a_hyphen_in_the_middle()
    {
        var (svc, _, _, _, _, _) = Build(codeLength: 10);

        var plain = await svc.RegenerateAsync(TenantContext.Default, KnownUserId);

        plain.Should().AllSatisfy(c => c.Should().Contain("-"));
    }

    [Fact]
    public async Task Regenerate_returned_codes_are_unique_within_the_batch()
    {
        var (svc, _, _, _, _, _) = Build();

        var plain = await svc.RegenerateAsync(TenantContext.Default, KnownUserId);

        plain.Distinct().Should().HaveCount(plain.Count, "RNG should not produce collisions in a batch this small");
    }

    // ── Redeem ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Redeem_rate_limited_returns_null()
    {
        var (svc, _, _, rate, _, _) = Build();
        var plain = await svc.RegenerateAsync(TenantContext.Default, KnownUserId);
        rate.AllowAcquire = false;

        var user = await svc.RedeemAsync(TenantContext.Default, KnownEmail, plain[0]);

        user.Should().BeNull();
    }

    [Fact]
    public async Task Redeem_unknown_email_returns_null_and_audits()
    {
        var (svc, _, _, _, audit, _) = Build();
        var plain = await svc.RegenerateAsync(TenantContext.Default, KnownUserId);

        var user = await svc.RedeemAsync(TenantContext.Default, "unknown@nowhere.test", plain[0]);

        user.Should().BeNull();
        audit.Events.Should().Contain(e => e.Type == AuditEventType.OtpInvalid && e.Detail == "recovery: unknown email");
    }

    [Fact]
    public async Task Redeem_wrong_code_returns_null()
    {
        var (svc, _, _, _, audit, _) = Build();
        await svc.RegenerateAsync(TenantContext.Default, KnownUserId);

        var user = await svc.RedeemAsync(TenantContext.Default, KnownEmail, "FAKEX-FAKEX");

        user.Should().BeNull();
        audit.Events.Should().Contain(e => e.Type == AuditEventType.OtpInvalid && e.Detail == "recovery: no matching code");
    }

    [Fact]
    public async Task Redeem_correct_code_returns_user_and_marks_consumed()
    {
        var (svc, _, codes, _, audit, _) = Build();
        var plain = await svc.RegenerateAsync(TenantContext.Default, KnownUserId);

        var user = await svc.RedeemAsync(TenantContext.Default, KnownEmail, plain[3]);

        user.Should().NotBeNull();
        user!.Id.Should().Be(KnownUserId);
        // Exactly one code should now be consumed (the matching one), the rest still active.
        codes.Saved.Count(c => c.ConsumedAt is not null).Should().Be(1);
        audit.Events.Should().Contain(e => e.Type == AuditEventType.OtpVerified && e.Detail == "recovery code redeemed");
    }

    [Fact]
    public async Task Redeem_is_single_use_consumed_code_cannot_be_replayed()
    {
        var (svc, _, _, _, _, _) = Build();
        var plain = await svc.RegenerateAsync(TenantContext.Default, KnownUserId);
        await svc.RedeemAsync(TenantContext.Default, KnownEmail, plain[0]);

        var second = await svc.RedeemAsync(TenantContext.Default, KnownEmail, plain[0]);

        second.Should().BeNull();
    }

    [Fact]
    public async Task Redeem_accepts_code_with_lowercase_hyphen_stripped_and_spaced_variants()
    {
        // Normalize() uppercases and strips non-alphanumerics — users typing the code with
        // different casing or formatting should still succeed.
        var (svc, _, _, _, _, _) = Build();
        var plain = await svc.RegenerateAsync(TenantContext.Default, KnownUserId);
        var noisy = plain[0].Replace("-", " ").ToLowerInvariant();

        var user = await svc.RedeemAsync(TenantContext.Default, KnownEmail, noisy);

        user.Should().NotBeNull();
    }

    // ── Count ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CountRemaining_decreases_after_each_redemption()
    {
        var (svc, _, _, _, _, _) = Build();
        var plain = await svc.RegenerateAsync(TenantContext.Default, KnownUserId);

        (await svc.CountRemainingAsync(TenantContext.Default, KnownUserId)).Should().Be(10);
        await svc.RedeemAsync(TenantContext.Default, KnownEmail, plain[0]);
        (await svc.CountRemainingAsync(TenantContext.Default, KnownUserId)).Should().Be(9);
        await svc.RedeemAsync(TenantContext.Default, KnownEmail, plain[1]);
        (await svc.CountRemainingAsync(TenantContext.Default, KnownUserId)).Should().Be(8);
    }
}

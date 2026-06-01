using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using TapInAuth.Auditing;
using TapInAuth.Core.Security;
using TapInAuth.Core.Services;
using TapInAuth.Core.Tests.Services.Fakes;
using TapInAuth.Delivery;
using TapInAuth.Options;
using TapInAuth.Tokens;
using Xunit;

namespace TapInAuth.Core.Tests.Services;

public class SmsOtpServiceTests
{
    private const string KnownPhone   = "+14155550100";
    private const string UnknownPhone = "+14155559999";
    private static readonly Guid KnownUserId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private static (SmsOtpService svc, FakeUserStore users, FakeOtpStore otps, FakeSmsSender sms, FakeRateLimiter rate, FakeAuditSink audit, FakeTimeProvider clock)
        Build(bool allowSignUp = true, bool smsSucceeds = true, bool rateLimited = false, int maxAttempts = 5)
    {
        var opts = Microsoft.Extensions.Options.Options.Create(new TapInAuthOptions
        {
            Methods = TapInAuthMethod.SmsOtp,
            Security =
            {
                OtpDigits = 6,
                OtpLifetime = TimeSpan.FromMinutes(5),
                MaxOtpAttempts = maxAttempts,
                AllowSignUp = allowSignUp,
            },
        });
        var users = new FakeUserStore();
        users.SeedWithPhone(KnownUserId, "alice@acme.test", KnownPhone, phoneVerified: false);
        var otps = new FakeOtpStore();
        var sms = new FakeSmsSender { ShouldSucceed = smsSucceeds };
        var rate = new FakeRateLimiter { AllowAcquire = !rateLimited };
        var audit = new FakeAuditSink();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-01T12:00:00Z"));
        var hasher = new TokenHasher(new byte[32]);
        var principalFactory = new TapInAuthClaimsPrincipalFactory();

        var svc = new SmsOtpService(opts, users, otps, sms, hasher, rate, audit, principalFactory, clock, NullLogger<SmsOtpService>.Instance);
        return (svc, users, otps, sms, rate, audit, clock);
    }

    // ── Issue ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Issue_invalid_phone_returns_Issued_without_sending_sms()
    {
        // Reason: don't tip off probing attackers that a format is invalid. Same redirect, same UX.
        var (svc, _, otps, sms, _, audit, _) = Build();

        var r = await svc.IssueAsync(TenantContext.Default, "not-a-phone");

        r.Should().Be(SmsOtpIssueResult.Issued);
        sms.Sent.Should().BeEmpty();
        otps.Saved.Should().BeEmpty();
        audit.Events.Should().ContainSingle(e => e.Type == AuditEventType.OtpInvalid && !e.Success);
    }

    [Fact]
    public async Task Issue_rate_limited_returns_RateLimited()
    {
        var (svc, _, otps, sms, _, audit, _) = Build(rateLimited: true);

        var r = await svc.IssueAsync(TenantContext.Default, KnownPhone);

        r.Should().Be(SmsOtpIssueResult.RateLimited);
        sms.Sent.Should().BeEmpty();
        otps.Saved.Should().BeEmpty();
        audit.Events.Should().Contain(e => e.Type == AuditEventType.RateLimitTriggered);
    }

    [Fact]
    public async Task Issue_unknown_phone_returns_Issued_silently_no_sms_no_audit_of_issuance()
    {
        // Critical enumeration-defense — unknown phone must not be distinguishable from known phone.
        var (svc, _, otps, sms, _, audit, _) = Build();

        var r = await svc.IssueAsync(TenantContext.Default, UnknownPhone);

        r.Should().Be(SmsOtpIssueResult.Issued);
        sms.Sent.Should().BeEmpty();
        otps.Saved.Should().BeEmpty();
        audit.Events.Should().NotContain(e => e.Type == AuditEventType.OtpIssued);
    }

    [Fact]
    public async Task Issue_known_phone_sends_sms_and_saves_otp_record()
    {
        var (svc, _, otps, sms, _, audit, _) = Build();

        var r = await svc.IssueAsync(TenantContext.Default, KnownPhone);

        r.Should().Be(SmsOtpIssueResult.Issued);
        sms.Sent.Should().ContainSingle();
        sms.Sent[0].To.Should().Be(KnownPhone);
        sms.Sent[0].Body.Should().Contain("sign-in code");
        otps.Saved.Should().ContainSingle();
        otps.Saved[0].Channel.Should().Be(OtpChannel.Sms);
        otps.Saved[0].UserId.Should().Be(KnownUserId);
        otps.Saved[0].Destination.Should().Be(KnownPhone);
        audit.Events.Should().Contain(e => e.Type == AuditEventType.OtpIssued && e.Success);
    }

    [Fact]
    public async Task Issue_sms_delivery_failure_returns_DeliveryFailed_but_still_audits()
    {
        var (svc, _, _, _, _, audit, _) = Build(smsSucceeds: false);

        var r = await svc.IssueAsync(TenantContext.Default, KnownPhone);

        r.Should().Be(SmsOtpIssueResult.DeliveryFailed);
        audit.Events.Should().Contain(e => e.Type == AuditEventType.OtpIssued && !e.Success);
    }

    // ── Verify ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Verify_invalid_phone_returns_Failure()
    {
        var (svc, _, _, _, _, _, _) = Build();

        var r = await svc.VerifyAsync(TenantContext.Default, "garbage", "123456");

        r.Outcome.Should().Be(AuthenticationOutcome.Failed);
    }

    [Fact]
    public async Task Verify_rate_limited_returns_RateLimited()
    {
        // Need a separate rate-limiter shape — we want issue to succeed and verify to be limited.
        var (svc, _, _, _, rate, _, _) = Build();
        await svc.IssueAsync(TenantContext.Default, KnownPhone);
        rate.AllowAcquire = false;

        var r = await svc.VerifyAsync(TenantContext.Default, KnownPhone, "123456");
        r.Outcome.Should().Be(AuthenticationOutcome.RateLimited);
    }

    [Fact]
    public async Task Verify_unknown_phone_returns_invalid_failure()
    {
        var (svc, _, _, _, _, _, _) = Build();

        var r = await svc.VerifyAsync(TenantContext.Default, UnknownPhone, "123456");

        r.Outcome.Should().Be(AuthenticationOutcome.Failed);
        r.FailureReason.Should().Be("invalid");
    }

    [Fact]
    public async Task Verify_no_active_otp_returns_expired()
    {
        var (svc, _, _, _, _, _, _) = Build();

        var r = await svc.VerifyAsync(TenantContext.Default, KnownPhone, "123456");

        r.Outcome.Should().Be(AuthenticationOutcome.Failed);
        r.FailureReason.Should().Be("expired");
    }

    [Fact]
    public async Task Verify_expired_otp_returns_expired()
    {
        var (svc, _, otps, _, _, _, clock) = Build();
        await svc.IssueAsync(TenantContext.Default, KnownPhone);
        clock.Advance(TimeSpan.FromMinutes(10));   // OTP lifetime is 5 minutes

        var r = await svc.VerifyAsync(TenantContext.Default, KnownPhone, "123456");

        r.Outcome.Should().Be(AuthenticationOutcome.Failed);
        r.FailureReason.Should().Be("expired");
    }

    [Fact]
    public async Task Verify_wrong_code_increments_attempt_counter()
    {
        var (svc, _, otps, _, _, audit, _) = Build();
        await svc.IssueAsync(TenantContext.Default, KnownPhone);

        var r = await svc.VerifyAsync(TenantContext.Default, KnownPhone, "000000");

        r.Outcome.Should().Be(AuthenticationOutcome.Failed);
        r.FailureReason.Should().Be("invalid");
        otps.Saved[0].AttemptCount.Should().Be(1);
        audit.Events.Should().Contain(e => e.Type == AuditEventType.OtpInvalid && e.Detail == "hash mismatch");
    }

    [Fact]
    public async Task Verify_exceeded_attempts_consumes_otp_and_fails()
    {
        var (svc, _, otps, _, _, audit, _) = Build(maxAttempts: 3);
        await svc.IssueAsync(TenantContext.Default, KnownPhone);
        otps.Saved[0].AttemptCount = 3;  // already at the cap

        var r = await svc.VerifyAsync(TenantContext.Default, KnownPhone, "000000");

        r.Outcome.Should().Be(AuthenticationOutcome.Failed);
        r.FailureReason.Should().Be("attempts_exceeded");
        otps.Saved[0].ConsumedAt.Should().NotBeNull();
        audit.Events.Should().Contain(e => e.Type == AuditEventType.OtpAttemptsExceeded);
    }

    [Fact]
    public async Task Verify_correct_code_succeeds_and_marks_phone_verified()
    {
        var (svc, users, otps, sms, _, audit, _) = Build();
        await svc.IssueAsync(TenantContext.Default, KnownPhone);
        var rawCode = sms.Sent[0].Body.Split(' ')[0];   // "<code> is your … code."

        var r = await svc.VerifyAsync(TenantContext.Default, KnownPhone, rawCode);

        r.Outcome.Should().Be(AuthenticationOutcome.Success);
        r.Method.Should().Be(TapInAuthMethod.SmsOtp);
        r.User!.PhoneVerified.Should().BeTrue();
        otps.Saved[0].ConsumedAt.Should().NotBeNull();
        users.PhoneVerifiedCalls.Should().Contain(KnownUserId);
        audit.Events.Should().Contain(e => e.Type == AuditEventType.OtpVerified && e.Success);
    }

    [Fact]
    public async Task Verify_strips_non_digit_characters_from_input_code()
    {
        var (svc, _, _, sms, _, _, _) = Build();
        await svc.IssueAsync(TenantContext.Default, KnownPhone);
        var rawCode = sms.Sent[0].Body.Split(' ')[0];
        var noisy = $" {rawCode[0]}-{rawCode[1]} {rawCode[2]}{rawCode[3]}.{rawCode[4]}.{rawCode[5]} ";

        var r = await svc.VerifyAsync(TenantContext.Default, KnownPhone, noisy);

        r.Outcome.Should().Be(AuthenticationOutcome.Success);
    }

    [Fact]
    public async Task Verify_consumed_otp_cannot_be_replayed()
    {
        var (svc, _, _, sms, _, _, _) = Build();
        await svc.IssueAsync(TenantContext.Default, KnownPhone);
        var rawCode = sms.Sent[0].Body.Split(' ')[0];

        var first = await svc.VerifyAsync(TenantContext.Default, KnownPhone, rawCode);
        var second = await svc.VerifyAsync(TenantContext.Default, KnownPhone, rawCode);

        first.Outcome.Should().Be(AuthenticationOutcome.Success);
        // The fake store excludes consumed records from FindActiveAsync, so this looks like "no active OTP".
        second.Outcome.Should().Be(AuthenticationOutcome.Failed);
        second.FailureReason.Should().Be("expired");
    }
}

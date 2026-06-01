using System.Globalization;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using TapInAuth.Auditing;
using TapInAuth.Core.Security;
using TapInAuth.Core.Services;
using TapInAuth.Core.Tests.Services.Fakes;
using TapInAuth.Options;
using TapInAuth.Tokens;
using Xunit;

namespace TapInAuth.Core.Tests.Services;

public class EmailOtpServiceTests
{
    private const string KnownEmail = "alice@acme.test";
    private const string NewEmail   = "newuser@acme.test";
    private static readonly Guid KnownUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private static (EmailOtpService svc, FakeUserStore users, FakeOtpStore otps, FakeEmailSender email, FakeRateLimiter rate, FakeAuditSink audit, FakeTimeProvider clock)
        Build(bool allowSignUp = true, bool emailSucceeds = true, bool rateLimited = false, int maxAttempts = 5)
    {
        var opts = Microsoft.Extensions.Options.Options.Create(new TapInAuthOptions
        {
            Methods = TapInAuthMethod.EmailOtp,
            Security =
            {
                OtpDigits = 6,
                OtpLifetime = TimeSpan.FromMinutes(5),
                MaxOtpAttempts = maxAttempts,
                AllowSignUp = allowSignUp,
            },
        });
        var users = new FakeUserStore();
        users.Seed(KnownUserId, KnownEmail);
        var otps = new FakeOtpStore();
        var email = new FakeEmailSender { ShouldSucceed = emailSucceeds };
        var rate = new FakeRateLimiter { AllowAcquire = !rateLimited };
        var audit = new FakeAuditSink();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-01T12:00:00Z", CultureInfo.InvariantCulture));
        var hasher = new TokenHasher(new byte[32]);
        var principalFactory = new TapInAuthClaimsPrincipalFactory();

        var svc = new EmailOtpService(opts, users, otps, email, hasher, rate, audit, principalFactory, clock, NullLogger<EmailOtpService>.Instance);
        return (svc, users, otps, email, rate, audit, clock);
    }

    // ── Issue ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Issue_rate_limited_returns_RateLimited_without_email()
    {
        var (svc, _, otps, email, _, audit, _) = Build(rateLimited: true);

        var r = await svc.IssueAsync(TenantContext.Default, KnownEmail);

        r.Should().Be(OtpIssueResult.RateLimited);
        email.Sent.Should().BeEmpty();
        otps.Saved.Should().BeEmpty();
        audit.Events.Should().Contain(e => e.Type == AuditEventType.RateLimitTriggered);
    }

    [Fact]
    public async Task Issue_unknown_email_signs_up_when_AllowSignUp_true()
    {
        var (svc, users, otps, email, _, audit, _) = Build(allowSignUp: true);

        var r = await svc.IssueAsync(TenantContext.Default, NewEmail);

        r.Should().Be(OtpIssueResult.Issued);
        users.CreatedEmails.Should().ContainSingle().Which.Should().Be(NewEmail);
        otps.Saved.Should().ContainSingle();
        email.Sent.Should().ContainSingle();
        audit.Events.Should().Contain(e => e.Type == AuditEventType.UserCreated);
        audit.Events.Should().Contain(e => e.Type == AuditEventType.OtpIssued && e.Success);
    }

    [Fact]
    public async Task Issue_unknown_email_drops_silently_when_AllowSignUp_false()
    {
        // Same-shape response prevents enumeration. No user created, no OTP saved, no email sent.
        var (svc, users, otps, email, _, audit, _) = Build(allowSignUp: false);

        var r = await svc.IssueAsync(TenantContext.Default, NewEmail);

        r.Should().Be(OtpIssueResult.Issued);
        users.CreatedEmails.Should().BeEmpty();
        otps.Saved.Should().BeEmpty();
        email.Sent.Should().BeEmpty();
        audit.Events.Should().NotContain(e => e.Type == AuditEventType.OtpIssued);
    }

    [Fact]
    public async Task Issue_known_email_sends_otp_and_saves_record()
    {
        var (svc, _, otps, email, _, _, _) = Build();

        var r = await svc.IssueAsync(TenantContext.Default, KnownEmail);

        r.Should().Be(OtpIssueResult.Issued);
        email.Sent.Should().ContainSingle();
        email.Sent[0].To.Should().Be(KnownEmail);
        email.Sent[0].Subject.Should().Contain("sign-in code");
        otps.Saved.Should().ContainSingle();
        otps.Saved[0].Channel.Should().Be(OtpChannel.Email);
        otps.Saved[0].UserId.Should().Be(KnownUserId);
    }

    [Fact]
    public async Task Issue_normalizes_email_case_and_whitespace()
    {
        var (svc, _, otps, email, _, _, _) = Build();

        await svc.IssueAsync(TenantContext.Default, "  ALICE@Acme.TEST  ");

        otps.Saved.Should().ContainSingle();
        otps.Saved[0].UserId.Should().Be(KnownUserId);   // matched the seeded lowercase alice@acme.test
        email.Sent.Should().ContainSingle();
        email.Sent[0].To.Should().Be(KnownEmail);
    }

    [Fact]
    public async Task Issue_email_delivery_failure_returns_DeliveryFailed()
    {
        var (svc, _, _, _, _, audit, _) = Build(emailSucceeds: false);

        var r = await svc.IssueAsync(TenantContext.Default, KnownEmail);

        r.Should().Be(OtpIssueResult.DeliveryFailed);
        audit.Events.Should().Contain(e => e.Type == AuditEventType.OtpIssued && !e.Success);
    }

    // ── Verify ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Verify_unknown_email_fails_invalid()
    {
        var (svc, _, _, _, _, audit, _) = Build();

        var r = await svc.VerifyAsync(TenantContext.Default, NewEmail, "123456");

        r.Outcome.Should().Be(AuthenticationOutcome.Failed);
        r.FailureReason.Should().Be("invalid");
        audit.Events.Should().Contain(e => e.Type == AuditEventType.OtpInvalid && e.Detail == "unknown email");
    }

    [Fact]
    public async Task Verify_expired_otp_returns_expired()
    {
        var (svc, _, _, _, _, _, clock) = Build();
        await svc.IssueAsync(TenantContext.Default, KnownEmail);
        clock.Advance(TimeSpan.FromMinutes(10));

        var r = await svc.VerifyAsync(TenantContext.Default, KnownEmail, "000000");

        r.FailureReason.Should().Be("expired");
    }

    [Fact]
    public async Task Verify_correct_code_succeeds_marks_email_verified_and_returns_principal()
    {
        var (svc, users, otps, email, _, audit, _) = Build();
        await svc.IssueAsync(TenantContext.Default, KnownEmail);
        // The subject line begins with the code: "<code> is your sign-in code" — simpler and more reliable
        // than parsing the body.
        var rawCode = email.Sent[0].Subject.Split(' ')[0];

        var r = await svc.VerifyAsync(TenantContext.Default, KnownEmail, rawCode);

        r.Outcome.Should().Be(AuthenticationOutcome.Success);
        r.Method.Should().Be(TapInAuthMethod.EmailOtp);
        r.User!.EmailVerified.Should().BeTrue();
        users.EmailVerifiedCalls.Should().Contain(KnownUserId);
        otps.Saved[0].ConsumedAt.Should().NotBeNull();
        audit.Events.Should().Contain(e => e.Type == AuditEventType.OtpVerified && e.Success);
    }

    [Fact]
    public async Task Verify_wrong_code_increments_attempts()
    {
        var (svc, _, otps, _, _, _, _) = Build();
        await svc.IssueAsync(TenantContext.Default, KnownEmail);

        var r1 = await svc.VerifyAsync(TenantContext.Default, KnownEmail, "000000");
        var r2 = await svc.VerifyAsync(TenantContext.Default, KnownEmail, "000001");

        r1.Outcome.Should().Be(AuthenticationOutcome.Failed);
        r2.Outcome.Should().Be(AuthenticationOutcome.Failed);
        otps.Saved[0].AttemptCount.Should().Be(2);
    }

    [Fact]
    public async Task Verify_attempts_exceeded_consumes_otp()
    {
        var (svc, _, otps, _, _, audit, _) = Build(maxAttempts: 2);
        await svc.IssueAsync(TenantContext.Default, KnownEmail);
        otps.Saved[0].AttemptCount = 2;

        var r = await svc.VerifyAsync(TenantContext.Default, KnownEmail, "000000");

        r.FailureReason.Should().Be("attempts_exceeded");
        otps.Saved[0].ConsumedAt.Should().NotBeNull();
        audit.Events.Should().Contain(e => e.Type == AuditEventType.OtpAttemptsExceeded);
    }
}

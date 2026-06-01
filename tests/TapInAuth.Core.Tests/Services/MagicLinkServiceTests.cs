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

public class MagicLinkServiceTests
{
    private const string KnownEmail = "alice@acme.test";
    private const string NewEmail   = "newuser@acme.test";
    private const string Template   = "https://test.local/auth/verify?id={tokenId}&t={token}";
    private static readonly Guid KnownUserId = Guid.Parse("ffffffff-1111-2222-3333-444444444444");

    private static (MagicLinkService svc, FakeUserStore users, FakeMagicLinkTokenStore tokens, FakeEmailSender email, FakeRateLimiter rate, FakeAuditSink audit, FakeTimeProvider clock)
        Build(bool allowSignUp = true, bool emailSucceeds = true, bool rateLimited = false)
    {
        var opts = Microsoft.Extensions.Options.Options.Create(new TapInAuthOptions
        {
            Methods = TapInAuthMethod.MagicLink,
            Security =
            {
                MagicLinkLifetime = TimeSpan.FromMinutes(10),
                AllowSignUp = allowSignUp,
            },
        });
        var users = new FakeUserStore();
        users.Seed(KnownUserId, KnownEmail);
        var tokens = new FakeMagicLinkTokenStore();
        var email = new FakeEmailSender { ShouldSucceed = emailSucceeds };
        var rate = new FakeRateLimiter { AllowAcquire = !rateLimited };
        var audit = new FakeAuditSink();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-01T12:00:00Z"));
        var hasher = new TokenHasher(new byte[32]);
        var principalFactory = new TapInAuthClaimsPrincipalFactory();

        var svc = new MagicLinkService(opts, users, tokens, email, hasher, rate, audit, principalFactory, clock, NullLogger<MagicLinkService>.Instance);
        return (svc, users, tokens, email, rate, audit, clock);
    }

    /// <summary>Pull the raw token out of the URL we emitted in the email body.</summary>
    private static (Guid tokenId, string rawToken) ExtractToken(FakeEmailSender email)
    {
        var body = email.Sent[0].PlainTextBody;
        var urlStart = body.IndexOf("https://", StringComparison.Ordinal);
        var urlEnd = body.IndexOf('\n', urlStart);
        var url = body[urlStart..(urlEnd < 0 ? body.Length : urlEnd)].Trim();
        // Avoid System.Web here — parse the query manually so the test project doesn't need a framework reference.
        var query = new Uri(url).Query.TrimStart('?');
        var parts = query.Split('&').Select(kv => kv.Split('=', 2)).ToDictionary(p => p[0], p => p[1]);
        return (Guid.Parse(parts["id"]), parts["t"]);
    }

    // ── Issue ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Issue_rate_limited_returns_RateLimited()
    {
        var (svc, _, tokens, email, _, audit, _) = Build(rateLimited: true);

        var r = await svc.IssueAsync(TenantContext.Default, KnownEmail, Template);

        r.Should().Be(MagicLinkIssueResult.RateLimited);
        email.Sent.Should().BeEmpty();
        tokens.Saved.Should().BeEmpty();
        audit.Events.Should().Contain(e => e.Type == AuditEventType.RateLimitTriggered);
    }

    [Fact]
    public async Task Issue_unknown_email_signs_up_when_AllowSignUp_true()
    {
        var (svc, users, tokens, email, _, _, _) = Build(allowSignUp: true);

        var r = await svc.IssueAsync(TenantContext.Default, NewEmail, Template);

        r.Should().Be(MagicLinkIssueResult.Issued);
        users.CreatedEmails.Should().ContainSingle().Which.Should().Be(NewEmail);
        tokens.Saved.Should().ContainSingle();
        email.Sent.Should().ContainSingle();
    }

    [Fact]
    public async Task Issue_unknown_email_drops_silently_when_AllowSignUp_false()
    {
        var (svc, users, tokens, email, _, _, _) = Build(allowSignUp: false);

        var r = await svc.IssueAsync(TenantContext.Default, NewEmail, Template);

        r.Should().Be(MagicLinkIssueResult.Issued);
        users.CreatedEmails.Should().BeEmpty();
        tokens.Saved.Should().BeEmpty();
        email.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Issue_known_email_produces_url_with_token_id_and_token()
    {
        var (svc, _, tokens, email, _, _, _) = Build();

        await svc.IssueAsync(TenantContext.Default, KnownEmail, Template);

        var (tokenId, rawToken) = ExtractToken(email);
        tokenId.Should().Be(tokens.Saved[0].Id);
        rawToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Issue_persists_return_url_for_later_redirect()
    {
        var (svc, _, tokens, _, _, _, _) = Build();

        await svc.IssueAsync(TenantContext.Default, KnownEmail, Template, returnUrl: "/dashboard");

        tokens.Saved[0].ReturnUrl.Should().Be("/dashboard");
    }

    // ── Redeem ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Redeem_unknown_token_returns_invalid()
    {
        var (svc, _, _, _, _, audit, _) = Build();

        var r = await svc.RedeemAsync(TenantContext.Default, Guid.NewGuid(), "anything");

        r.Outcome.Should().Be(AuthenticationOutcome.Failed);
        r.FailureReason.Should().Be("invalid");
        audit.Events.Should().Contain(e => e.Type == AuditEventType.MagicLinkInvalid && e.Detail == "token not found");
    }

    [Fact]
    public async Task Redeem_already_consumed_token_returns_consumed()
    {
        var (svc, _, _, email, _, _, _) = Build();
        await svc.IssueAsync(TenantContext.Default, KnownEmail, Template);
        var (tokenId, rawToken) = ExtractToken(email);
        await svc.RedeemAsync(TenantContext.Default, tokenId, rawToken);

        var second = await svc.RedeemAsync(TenantContext.Default, tokenId, rawToken);

        second.Outcome.Should().Be(AuthenticationOutcome.Failed);
        second.FailureReason.Should().Be("consumed");
    }

    [Fact]
    public async Task Redeem_expired_token_returns_expired()
    {
        var (svc, _, _, email, _, _, clock) = Build();
        await svc.IssueAsync(TenantContext.Default, KnownEmail, Template);
        var (tokenId, rawToken) = ExtractToken(email);
        clock.Advance(TimeSpan.FromMinutes(15));

        var r = await svc.RedeemAsync(TenantContext.Default, tokenId, rawToken);

        r.Outcome.Should().Be(AuthenticationOutcome.Failed);
        r.FailureReason.Should().Be("expired");
    }

    [Fact]
    public async Task Redeem_tampered_token_returns_invalid()
    {
        var (svc, _, _, email, _, audit, _) = Build();
        await svc.IssueAsync(TenantContext.Default, KnownEmail, Template);
        var (tokenId, rawToken) = ExtractToken(email);
        var tampered = rawToken[..^1] + (rawToken[^1] == 'A' ? 'B' : 'A');

        var r = await svc.RedeemAsync(TenantContext.Default, tokenId, tampered);

        r.Outcome.Should().Be(AuthenticationOutcome.Failed);
        r.FailureReason.Should().Be("invalid");
        audit.Events.Should().Contain(e => e.Type == AuditEventType.MagicLinkInvalid && e.Detail == "hash mismatch");
    }

    [Fact]
    public async Task Redeem_valid_token_succeeds_marks_consumed_marks_email_verified()
    {
        var (svc, users, tokens, email, _, audit, _) = Build();
        await svc.IssueAsync(TenantContext.Default, KnownEmail, Template);
        var (tokenId, rawToken) = ExtractToken(email);

        var r = await svc.RedeemAsync(TenantContext.Default, tokenId, rawToken);

        r.Outcome.Should().Be(AuthenticationOutcome.Success);
        r.Method.Should().Be(TapInAuthMethod.MagicLink);
        r.User!.EmailVerified.Should().BeTrue();
        users.EmailVerifiedCalls.Should().Contain(KnownUserId);
        tokens.Saved[0].ConsumedAt.Should().NotBeNull();
        audit.Events.Should().Contain(e => e.Type == AuditEventType.MagicLinkRedeemed && e.Success);
    }

    [Fact]
    public async Task Redeem_returns_stored_returnUrl_for_post_signin_redirect()
    {
        var (svc, _, _, email, _, _, _) = Build();
        await svc.IssueAsync(TenantContext.Default, KnownEmail, Template, returnUrl: "/orders/42");
        var (tokenId, rawToken) = ExtractToken(email);

        var r = await svc.RedeemAsync(TenantContext.Default, tokenId, rawToken);

        r.ReturnUrl.Should().Be("/orders/42");
    }
}

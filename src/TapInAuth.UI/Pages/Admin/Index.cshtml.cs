using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TapInAuth.Auditing;
using TapInAuth.Tenancy;

namespace TapInAuth.UI.Pages.Admin;

[Authorize(Policy = "TapInAuth.Admin")]
public class IndexModel : PageModel
{
    private readonly IAuditQuery? _audit;
    private readonly ITenantResolver _tenantResolver;

    public IndexModel(ITenantResolver tenantResolver, IAuditQuery? audit = null)
    {
        _tenantResolver = tenantResolver;
        _audit = audit;
    }

    public string TenantId { get; private set; } = TenantContext.DefaultTenantId;
    public bool HasPersistentAudit { get; private set; }
    public int EventsLast24h { get; private set; }
    public int SuccessfulSignIns24h { get; private set; }
    public int FailedSignIns24h { get; private set; }
    public int RateLimits24h { get; private set; }
    public IReadOnlyList<AuditEvent> Recent { get; private set; } = [];

    public async Task OnGet(CancellationToken cancellationToken)
    {
        var tenant = (await _tenantResolver.ResolveAsync(cancellationToken)) ?? TenantContext.Default;
        TenantId = tenant.Id;
        HasPersistentAudit = _audit is not null;

        if (_audit is null)
        {
            return;
        }

        var since = DateTimeOffset.UtcNow.AddHours(-24);
        var window = await _audit.ListRecentAsync(tenant, take: 1000, since: since, cancellationToken).ConfigureAwait(false);
        EventsLast24h = window.Count;
        SuccessfulSignIns24h = window.Count(e => e.Success && (
            e.Type == AuditEventType.MagicLinkRedeemed ||
            e.Type == AuditEventType.OtpVerified ||
            e.Type == AuditEventType.CredentialAsserted ||
            e.Type == AuditEventType.SignInCompleted));
        FailedSignIns24h = window.Count(e => !e.Success && (
            e.Type == AuditEventType.MagicLinkInvalid ||
            e.Type == AuditEventType.MagicLinkExpired ||
            e.Type == AuditEventType.OtpInvalid ||
            e.Type == AuditEventType.OtpAttemptsExceeded ||
            e.Type == AuditEventType.SignInFailed));
        RateLimits24h = window.Count(e => e.Type == AuditEventType.RateLimitTriggered);
        Recent = await _audit.ListRecentAsync(tenant, take: 10, since: null, cancellationToken).ConfigureAwait(false);
    }
}

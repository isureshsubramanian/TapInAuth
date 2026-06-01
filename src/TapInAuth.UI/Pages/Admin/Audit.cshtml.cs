using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TapInAuth.Auditing;
using TapInAuth.Tenancy;

namespace TapInAuth.UI.Pages.Admin;

[Authorize(Policy = "TapInAuth.Admin")]
public class AuditModel : PageModel
{
    private readonly IAuditQuery? _audit;
    private readonly ITenantResolver _tenantResolver;

    public AuditModel(ITenantResolver tenantResolver, IAuditQuery? audit = null)
    {
        _tenantResolver = tenantResolver;
        _audit = audit;
    }

    public string TenantId { get; private set; } = TenantContext.DefaultTenantId;
    public bool HasPersistentAudit { get; private set; }
    public IReadOnlyList<AuditEvent> Events { get; private set; } = [];

    public async Task OnGet(CancellationToken cancellationToken)
    {
        var tenant = (await _tenantResolver.ResolveAsync(cancellationToken)) ?? TenantContext.Default;
        TenantId = tenant.Id;
        HasPersistentAudit = _audit is not null;
        if (_audit is null)
        {
            return;
        }
        Events = await _audit.ListRecentAsync(tenant, take: 200, since: null, cancellationToken).ConfigureAwait(false);
    }
}

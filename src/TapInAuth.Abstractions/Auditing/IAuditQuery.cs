namespace TapInAuth.Auditing;

/// <summary>
/// Read-side counterpart to <see cref="IAuditSink"/>. Implementations that persist events
/// (e.g., the EF Core sink) also implement this to power the admin audit feed.
/// Logging-only sinks return empty results.
/// </summary>
public interface IAuditQuery
{
    /// <summary>List the most recent audit events for a tenant, newest first.</summary>
    /// <param name="tenant">The tenant to scope to.</param>
    /// <param name="take">Maximum number of events to return.</param>
    /// <param name="since">Optional inclusive lower bound on event timestamp.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<AuditEvent>> ListRecentAsync(
        TenantContext tenant,
        int take = 200,
        DateTimeOffset? since = null,
        CancellationToken cancellationToken = default);
}

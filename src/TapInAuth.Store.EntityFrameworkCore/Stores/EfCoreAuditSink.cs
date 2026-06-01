using Microsoft.EntityFrameworkCore;
using TapInAuth.Auditing;
using TapInAuth.Store.EntityFrameworkCore.Entities;

namespace TapInAuth.Store.EntityFrameworkCore.Stores;

/// <summary>
/// Persistent EF Core implementation of both <see cref="IAuditSink"/> (write) and
/// <see cref="IAuditQuery"/> (read). Drop-in replacement for the default LoggingAuditSink when
/// hosts want a browsable audit feed via the admin UI.
/// </summary>
public sealed class EfCoreAuditSink<TContext> : IAuditSink, IAuditQuery where TContext : DbContext
{
    private readonly TContext _db;

    /// <summary>Construct the sink.</summary>
    public EfCoreAuditSink(TContext db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc />
    public async Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        _db.Set<AuditEventEntity>().Add(new AuditEventEntity
        {
            Timestamp = auditEvent.Timestamp,
            TenantId = auditEvent.TenantId,
            Type = auditEvent.Type,
            UserId = auditEvent.UserId,
            Email = auditEvent.Email,
            IpAddress = auditEvent.IpAddress,
            UserAgent = auditEvent.UserAgent,
            Detail = auditEvent.Detail,
            Success = auditEvent.Success,
        });
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditEvent>> ListRecentAsync(
        TenantContext tenant, int take = 200, DateTimeOffset? since = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        var query = _db.Set<AuditEventEntity>()
            .AsNoTracking()
            .Where(x => x.TenantId == tenant.Id);
        if (since is not null)
        {
            query = query.Where(x => x.Timestamp >= since.Value);
        }
        var rows = await query
            .OrderByDescending(x => x.Id)
            .Take(Math.Clamp(take, 1, 1000))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows
            .Select(e => new AuditEvent(e.Timestamp, e.TenantId, e.Type, e.UserId, e.Email, e.IpAddress, e.UserAgent, e.Detail, e.Success))
            .ToList();
    }
}

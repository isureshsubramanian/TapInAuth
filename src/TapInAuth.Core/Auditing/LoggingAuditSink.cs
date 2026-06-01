using Microsoft.Extensions.Logging;
using TapInAuth.Auditing;

namespace TapInAuth.Core.Auditing;

/// <summary>
/// Default <see cref="IAuditSink"/> that writes structured audit events through <see cref="ILogger"/>.
/// Production deployments typically replace this with a sink that writes to a SIEM, database, or message bus.
/// </summary>
public sealed class LoggingAuditSink(ILogger<LoggingAuditSink> logger) : IAuditSink
{
    private readonly ILogger<LoggingAuditSink> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        // Structured properties so logs are queryable.
        _logger.Log(
            auditEvent.Success ? LogLevel.Information : LogLevel.Warning,
            "TapInAuth audit: {Type} success={Success} tenant={Tenant} user={UserId} email={Email} ip={Ip} ua={UserAgent} detail={Detail}",
            auditEvent.Type,
            auditEvent.Success,
            auditEvent.TenantId,
            auditEvent.UserId ?? "-",
            auditEvent.Email ?? "-",
            auditEvent.IpAddress ?? "-",
            auditEvent.UserAgent ?? "-",
            auditEvent.Detail ?? "-");
        return Task.CompletedTask;
    }
}

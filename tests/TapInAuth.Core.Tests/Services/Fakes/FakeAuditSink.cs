using TapInAuth.Auditing;

namespace TapInAuth.Core.Tests.Services.Fakes;

/// <summary>Captures every audit event so tests can assert about audit behavior.</summary>
public sealed class FakeAuditSink : IAuditSink
{
    public List<AuditEvent> Events { get; } = new();

    public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        Events.Add(auditEvent);
        return Task.CompletedTask;
    }
}

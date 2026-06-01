namespace TapInAuth.Auditing;

/// <summary>
/// Receives structured audit events from TapInAuth.
/// Default implementation logs to <c>ILogger</c>; hosts can plug in their own sink to write to a SIEM,
/// database table, or message broker.
/// </summary>
public interface IAuditSink
{
    /// <summary>Record an audit event. Implementations should be non-blocking — fire-and-forget is acceptable.</summary>
    Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}

/// <summary>A structured audit event emitted by TapInAuth.</summary>
public sealed record AuditEvent(
    DateTimeOffset Timestamp,
    string TenantId,
    AuditEventType Type,
    string? UserId,
    string? Email,
    string? IpAddress,
    string? UserAgent,
    string? Detail,
    bool Success);

/// <summary>The category of an audit event.</summary>
public enum AuditEventType
{
    UserCreated,
    SignInStarted,
    SignInCompleted,
    SignInFailed,
    MagicLinkIssued,
    MagicLinkRedeemed,
    MagicLinkExpired,
    MagicLinkInvalid,
    OtpIssued,
    OtpVerified,
    OtpInvalid,
    OtpAttemptsExceeded,
    CredentialRegistered,
    CredentialAsserted,
    CredentialRevoked,
    RateLimitTriggered,
    SignedOut,
}

namespace TapInAuth.Risk;

/// <summary>
/// Optional hook for risk-signal providers. Implementations can call out to Cloudflare Turnstile,
/// hCaptcha, Datadog Cloud SIEM, Sift, or an in-house service to score a sign-in attempt.
/// </summary>
public interface IRiskSignalProvider
{
    /// <summary>Evaluate the risk of the given attempt.</summary>
    Task<RiskAssessment> EvaluateAsync(RiskContext context, CancellationToken cancellationToken = default);
}

/// <summary>The context surrounding a sign-in attempt.</summary>
public sealed record RiskContext(
    TenantContext Tenant,
    string Email,
    string? IpAddress,
    string? UserAgent,
    IReadOnlyDictionary<string, string>? Headers = null,
    IReadOnlyDictionary<string, object>? Extra = null);

/// <summary>Outcome of a risk evaluation.</summary>
public sealed record RiskAssessment(
    RiskLevel Level,
    string? Reason = null,
    bool RequireStepUp = false);

/// <summary>Risk levels. <see cref="Block"/> aborts the attempt; <see cref="Challenge"/> forces step-up.</summary>
public enum RiskLevel
{
    Low,
    Medium,
    High,
    Challenge,
    Block,
}

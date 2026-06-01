using System.Security.Claims;

namespace TapInAuth;

/// <summary>The outcome of a TapInAuth authentication attempt.</summary>
public sealed record AuthenticationResult(
    AuthenticationOutcome Outcome,
    ClaimsPrincipal? Principal = null,
    TapInAuthUser? User = null,
    string? FailureReason = null,
    string? ReturnUrl = null,
    TapInAuthMethod Method = TapInAuthMethod.None)
{
    /// <summary>Whether authentication succeeded.</summary>
    public bool Succeeded => Outcome == AuthenticationOutcome.Success;

    /// <summary>Create a success result.</summary>
    public static AuthenticationResult Success(ClaimsPrincipal principal, TapInAuthUser user, TapInAuthMethod method, string? returnUrl = null)
        => new(AuthenticationOutcome.Success, principal, user, null, returnUrl, method);

    /// <summary>Create a failure result with an opaque reason for logging (never shown to the user verbatim).</summary>
    public static AuthenticationResult Failure(string reason, TapInAuthMethod method = TapInAuthMethod.None)
        => new(AuthenticationOutcome.Failed, null, null, reason, null, method);

    /// <summary>Create a result indicating the user must complete a follow-up step (e.g., enter an OTP).</summary>
    public static AuthenticationResult PendingChallenge(TapInAuthMethod method)
        => new(AuthenticationOutcome.PendingChallenge, null, null, null, null, method);

    /// <summary>Create a result indicating the request was rate-limited.</summary>
    public static AuthenticationResult RateLimited()
        => new(AuthenticationOutcome.RateLimited);
}

/// <summary>The possible outcomes of an authentication attempt.</summary>
public enum AuthenticationOutcome
{
    /// <summary>Authentication succeeded; <c>Principal</c> and <c>User</c> are populated.</summary>
    Success,
    /// <summary>Authentication failed (invalid token, expired, no matching user, etc.).</summary>
    Failed,
    /// <summary>A first step succeeded; a follow-up is required (e.g., enter the OTP that was just sent).</summary>
    PendingChallenge,
    /// <summary>The request was rate-limited and not processed.</summary>
    RateLimited,
}

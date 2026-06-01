namespace TapInAuth.Delivery;

/// <summary>
/// Sends transactional emails on behalf of TapInAuth.
/// Implementations live in provider sub-packages: <c>TapInAuth.Email.Smtp</c>, <c>TapInAuth.Email.SendGrid</c>, etc.
/// </summary>
public interface IEmailSender
{
    /// <summary>Send an email. Returns true if the provider accepted the message; throws on hard failure.</summary>
    Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>An outbound email message.</summary>
public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string PlainTextBody,
    string? From = null,
    string? FromDisplayName = null,
    string? ReplyTo = null,
    IReadOnlyDictionary<string, string>? Headers = null);

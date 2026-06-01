namespace TapInAuth.Delivery;

/// <summary>
/// Sends transactional SMS messages on behalf of TapInAuth.
/// Implementations live in provider sub-packages: <c>TapInAuth.Sms.Twilio</c>, <c>TapInAuth.Sms.MessageBird</c>, etc.
/// </summary>
public interface ISmsSender
{
    /// <summary>Send an SMS. Returns true if the provider accepted the message; throws on hard failure.</summary>
    Task<bool> SendAsync(SmsMessage message, CancellationToken cancellationToken = default);
}

/// <summary>An outbound SMS message.</summary>
public sealed record SmsMessage(
    string To,
    string Body,
    string? From = null);

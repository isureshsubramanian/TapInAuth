using System.ComponentModel.DataAnnotations;

namespace TapInAuth.Sms.Twilio;

/// <summary>Twilio SMS connection + sender options.</summary>
public sealed class TwilioSmsOptions
{
    /// <summary>Default options section name for binding (<c>"TapInAuth:Twilio"</c>).</summary>
    public const string SectionName = "TapInAuth:Twilio";

    /// <summary>Twilio Account SID (starts with <c>AC...</c>).</summary>
    [Required]
    public string AccountSid { get; set; } = string.Empty;

    /// <summary>Twilio Auth Token. Keep in a secret store; never check into source control.</summary>
    [Required]
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>
    /// The sender phone number in E.164 format (e.g., <c>+15551234567</c>) OR a Messaging Service SID
    /// (starts with <c>MG...</c>). One of <see cref="FromNumber"/> or <see cref="MessagingServiceSid"/> must be set.
    /// </summary>
    public string? FromNumber { get; set; }

    /// <summary>
    /// Twilio Messaging Service SID. Preferred over <see cref="FromNumber"/> for production —
    /// Twilio routes to the best sender automatically.
    /// </summary>
    public string? MessagingServiceSid { get; set; }
}

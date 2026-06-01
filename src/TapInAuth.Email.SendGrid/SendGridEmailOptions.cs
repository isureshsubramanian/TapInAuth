using System.ComponentModel.DataAnnotations;

namespace TapInAuth.Email.SendGrid;

/// <summary>SendGrid HTTP API options.</summary>
public sealed class SendGridEmailOptions
{
    /// <summary>Default options section name for binding (<c>"TapInAuth:SendGrid"</c>).</summary>
    public const string SectionName = "TapInAuth:SendGrid";

    /// <summary>SendGrid API key (starts with <c>SG.</c>).</summary>
    [Required]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Default sender address. Must be a verified sender or be on an authenticated domain.</summary>
    [Required]
    [EmailAddress]
    public string FromAddress { get; set; } = "no-reply@example.com";

    /// <summary>Default sender display name.</summary>
    public string FromDisplayName { get; set; } = "TapInAuth";

    /// <summary>Disable click tracking. Recommended for magic-link emails so the wrapper URL doesn't burn the single-use token.</summary>
    public bool DisableClickTracking { get; set; } = true;
}

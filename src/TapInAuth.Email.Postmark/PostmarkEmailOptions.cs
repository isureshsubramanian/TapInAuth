using System.ComponentModel.DataAnnotations;

namespace TapInAuth.Email.Postmark;

/// <summary>Postmark HTTP API options.</summary>
public sealed class PostmarkEmailOptions
{
    /// <summary>Default options section name for binding (<c>"TapInAuth:Postmark"</c>).</summary>
    public const string SectionName = "TapInAuth:Postmark";

    /// <summary>Postmark server token (NOT the account token).</summary>
    [Required]
    public string ServerToken { get; set; } = string.Empty;

    /// <summary>Default sender address. Must be a verified sender signature or be on a confirmed domain.</summary>
    [Required]
    [EmailAddress]
    public string FromAddress { get; set; } = "no-reply@example.com";

    /// <summary>Default sender display name.</summary>
    public string FromDisplayName { get; set; } = "TapInAuth";

    /// <summary>
    /// Postmark message stream. Auth emails should use a Transactional stream (the default
    /// <c>"outbound"</c> is transactional). Using a Broadcast stream causes Postmark to reject.
    /// </summary>
    public string MessageStream { get; set; } = "outbound";

    /// <summary>
    /// Disable Postmark click tracking. Strongly recommended for magic-link emails — Postmark's
    /// link wrapper would otherwise rewrite the URL through a tracking redirect, and pre-fetchers
    /// hitting that redirect would consume the single-use token before the human clicked.
    /// </summary>
    public bool DisableClickTracking { get; set; } = true;
}

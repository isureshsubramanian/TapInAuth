using System.ComponentModel.DataAnnotations;

namespace TapInAuth.Email.Ses;

/// <summary>Amazon SES (SESv2) options.</summary>
public sealed class SesEmailOptions
{
    /// <summary>Default options section name for binding (<c>"TapInAuth:Ses"</c>).</summary>
    public const string SectionName = "TapInAuth:Ses";

    /// <summary>AWS region (e.g. <c>"us-east-1"</c>). When unset, the SDK's default credential resolution determines the region.</summary>
    public string? Region { get; set; }

    /// <summary>
    /// AWS access key. Leave null in AWS-hosted environments — the SDK will pick up IAM role credentials.
    /// Set explicitly only for local development.
    /// </summary>
    public string? AccessKey { get; set; }

    /// <summary>AWS secret key. Pairs with <see cref="AccessKey"/>.</summary>
    public string? SecretKey { get; set; }

    /// <summary>Default sender address. Must be a verified identity in SES (sandbox) or be on a verified domain.</summary>
    [Required]
    [EmailAddress]
    public string FromAddress { get; set; } = "no-reply@example.com";

    /// <summary>Default sender display name.</summary>
    public string FromDisplayName { get; set; } = "TapInAuth";

    /// <summary>Optional SES configuration set (for event publishing / IP pool routing).</summary>
    public string? ConfigurationSet { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace TapInAuth.Email.Smtp;

/// <summary>SMTP connection options.</summary>
public sealed class SmtpEmailOptions
{
    /// <summary>Default options section name for binding (<c>"TapInAuth:Smtp"</c>).</summary>
    public const string SectionName = "TapInAuth:Smtp";

    /// <summary>SMTP server host.</summary>
    [Required]
    public string Host { get; set; } = "localhost";

    /// <summary>SMTP server port.</summary>
    public int Port { get; set; } = 587;

    /// <summary>Username for SMTP AUTH (optional).</summary>
    public string? Username { get; set; }

    /// <summary>Password for SMTP AUTH (optional).</summary>
    public string? Password { get; set; }

    /// <summary>Use STARTTLS (recommended for port 587).</summary>
    public bool UseStartTls { get; set; } = true;

    /// <summary>Use implicit TLS (recommended for port 465).</summary>
    public bool UseImplicitTls { get; set; }

    /// <summary>Default sender address used when an <c>EmailMessage</c> does not specify one.</summary>
    [Required]
    [EmailAddress]
    public string FromAddress { get; set; } = "no-reply@example.com";

    /// <summary>Default sender display name.</summary>
    public string FromDisplayName { get; set; } = "TapInAuth";

    /// <summary>Connection timeout. Defaults to 30 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

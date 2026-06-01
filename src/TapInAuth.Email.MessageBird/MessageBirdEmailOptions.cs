using System.ComponentModel.DataAnnotations;

namespace TapInAuth.Email.MessageBird;

/// <summary>MessageBird (Bird) Email API options.</summary>
public sealed class MessageBirdEmailOptions
{
    /// <summary>Default options section name for binding (<c>"TapInAuth:MessageBird"</c>).</summary>
    public const string SectionName = "TapInAuth:MessageBird";

    /// <summary>Bird workspace access key (Bearer token).</summary>
    [Required]
    public string AccessKey { get; set; } = string.Empty;

    /// <summary>Bird workspace identifier (UUID).</summary>
    [Required]
    public string WorkspaceId { get; set; } = string.Empty;

    /// <summary>Bird email channel identifier (UUID) — the verified sending domain bound to this workspace.</summary>
    [Required]
    public string ChannelId { get; set; } = string.Empty;

    /// <summary>Default sender address. Must be on the verified domain configured for the channel.</summary>
    [Required]
    [EmailAddress]
    public string FromAddress { get; set; } = "no-reply@example.com";

    /// <summary>Default sender display name.</summary>
    public string FromDisplayName { get; set; } = "TapInAuth";

    /// <summary>
    /// Base URL for the Bird Channels API. Override only for testing against a mock server.
    /// The Bird API base is <c>https://api.bird.com</c>.
    /// </summary>
    public string ApiBaseUrl { get; set; } = "https://api.bird.com";
}

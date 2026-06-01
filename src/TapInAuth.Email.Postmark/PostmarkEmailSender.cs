using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostmarkDotNet;
using PostmarkDotNet.Model;
using TapInAuth.Delivery;

namespace TapInAuth.Email.Postmark;

/// <summary>
/// Postmark HTTP API <see cref="IEmailSender"/> implementation.
/// </summary>
/// <remarks>
/// Click-tracking defaults to off — Postmark would otherwise wrap magic-link URLs in a
/// tracking redirect, and email pre-fetchers (anti-virus scanners, link previews) following
/// that redirect would consume the single-use token before the human clicked.
/// </remarks>
public sealed class PostmarkEmailSender : IEmailSender
{
    private readonly IOptions<PostmarkEmailOptions> _options;
    private readonly PostmarkClient _client;
    private readonly ILogger<PostmarkEmailSender> _logger;

    /// <summary>Construct a Postmark sender.</summary>
    public PostmarkEmailSender(
        IOptions<PostmarkEmailOptions> options,
        PostmarkClient client,
        ILogger<PostmarkEmailSender> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();
        var opts = _options.Value;

        var fromAddress = message.From ?? opts.FromAddress;
        var fromDisplay = message.FromDisplayName ?? opts.FromDisplayName;
        var from = string.IsNullOrWhiteSpace(fromDisplay) ? fromAddress : $"{fromDisplay} <{fromAddress}>";

        var msg = new PostmarkMessage
        {
            From = from,
            To = message.To,
            Subject = message.Subject,
            HtmlBody = message.HtmlBody,
            TextBody = message.PlainTextBody,
            ReplyTo = message.ReplyTo,
            MessageStream = opts.MessageStream,
            TrackLinks = opts.DisableClickTracking ? LinkTrackingOptions.None : LinkTrackingOptions.HtmlAndText,
        };

        if (message.Headers is { Count: > 0 })
        {
            // HeaderCollection : List<MailHeader> — no (string,string) overload, must construct MailHeader.
            msg.Headers = new HeaderCollection();
            foreach (var (k, v) in message.Headers)
            {
                msg.Headers.Add(new MailHeader(k, v));
            }
        }

        try
        {
            var response = await _client.SendMessageAsync(msg).ConfigureAwait(false);
            if (response.Status == PostmarkStatus.Success)
            {
                return true;
            }

            _logger.LogError(
                "TapInAuth Postmark send to {To} failed: {ErrorCode} {Message}",
                message.To,
                response.ErrorCode,
                response.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TapInAuth Postmark send failed to {To}", message.To);
            return false;
        }
    }
}

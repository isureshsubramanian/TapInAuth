using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;
using TapInAuth.Delivery;

namespace TapInAuth.Email.SendGrid;

/// <summary>
/// SendGrid HTTP API <see cref="IEmailSender"/> implementation.
/// </summary>
/// <remarks>
/// Click-tracking is disabled by default for magic-link emails — SendGrid's link wrapper
/// would otherwise rewrite the URL through a tracking redirect, and pre-fetchers (anti-virus,
/// link previews) hitting that redirect would consume the single-use token before the human
/// clicked it. Hosts that want tracking can flip <see cref="SendGridEmailOptions.DisableClickTracking"/>.
/// </remarks>
public sealed class SendGridEmailSender : IEmailSender
{
    private readonly IOptions<SendGridEmailOptions> _options;
    private readonly ISendGridClient _client;
    private readonly ILogger<SendGridEmailSender> _logger;

    /// <summary>Construct a SendGrid sender.</summary>
    public SendGridEmailSender(
        IOptions<SendGridEmailOptions> options,
        ISendGridClient client,
        ILogger<SendGridEmailSender> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var opts = _options.Value;

        var from = new EmailAddress(message.From ?? opts.FromAddress, message.FromDisplayName ?? opts.FromDisplayName);
        var to = new EmailAddress(message.To);
        var mail = MailHelper.CreateSingleEmail(from, to, message.Subject, message.PlainTextBody, message.HtmlBody);

        if (!string.IsNullOrWhiteSpace(message.ReplyTo))
        {
            mail.ReplyTo = new EmailAddress(message.ReplyTo);
        }

        if (opts.DisableClickTracking)
        {
            mail.SetClickTracking(false, false);
        }

        if (message.Headers is { Count: > 0 })
        {
            foreach (var (k, v) in message.Headers)
            {
                mail.AddHeader(k, v);
            }
        }

        try
        {
            var response = await _client.SendEmailAsync(mail, cancellationToken).ConfigureAwait(false);
            if ((int)response.StatusCode >= 200 && (int)response.StatusCode < 300)
            {
                return true;
            }

            var body = await response.Body.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError("TapInAuth SendGrid send to {To} failed: {Status} {Body}", message.To, response.StatusCode, body);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TapInAuth SendGrid send failed to {To}", message.To);
            return false;
        }
    }
}

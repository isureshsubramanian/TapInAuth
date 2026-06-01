using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using TapInAuth.Delivery;

namespace TapInAuth.Email.Smtp;

/// <summary>SMTP <see cref="IEmailSender"/> implementation backed by MailKit.</summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly IOptions<SmtpEmailOptions> _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    /// <summary>Construct an SMTP sender.</summary>
    public SmtpEmailSender(IOptions<SmtpEmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var opts = _options.Value;

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(
            message.FromDisplayName ?? opts.FromDisplayName,
            message.From ?? opts.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        if (!string.IsNullOrWhiteSpace(message.ReplyTo))
        {
            mime.ReplyTo.Add(MailboxAddress.Parse(message.ReplyTo));
        }

        var body = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.PlainTextBody,
        };
        mime.Body = body.ToMessageBody();

        if (message.Headers is { Count: > 0 })
        {
            foreach (var (k, v) in message.Headers)
            {
                mime.Headers.Add(k, v);
            }
        }

        using var client = new SmtpClient { Timeout = (int)opts.Timeout.TotalMilliseconds };
        var secure = opts.UseImplicitTls
            ? SecureSocketOptions.SslOnConnect
            : opts.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
        try
        {
            await client.ConnectAsync(opts.Host, opts.Port, secure, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(opts.Username))
            {
                await client.AuthenticateAsync(opts.Username, opts.Password ?? string.Empty, cancellationToken).ConfigureAwait(false);
            }
            await client.SendAsync(mime, cancellationToken).ConfigureAwait(false);
            await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TapInAuth SMTP send failed to {To} via {Host}:{Port}", message.To, opts.Host, opts.Port);
            return false;
        }
    }
}

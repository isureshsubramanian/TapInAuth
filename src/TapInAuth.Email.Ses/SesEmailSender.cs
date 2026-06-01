using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TapInAuth.Delivery;

namespace TapInAuth.Email.Ses;

/// <summary>
/// Amazon SES v2 <see cref="IEmailSender"/> implementation.
/// </summary>
/// <remarks>
/// Uses the simple <c>SendEmail</c> API with an inline <c>EmailContent.Simple</c> payload (subject + html + text).
/// For DKIM signing, IP-pool routing, or custom MAIL FROM, set <see cref="SesEmailOptions.ConfigurationSet"/>
/// — SES handles those concerns server-side based on the configuration set.
/// </remarks>
public sealed class SesEmailSender : IEmailSender
{
    private readonly IOptions<SesEmailOptions> _options;
    private readonly IAmazonSimpleEmailServiceV2 _client;
    private readonly ILogger<SesEmailSender> _logger;

    /// <summary>Construct an SES sender.</summary>
    public SesEmailSender(
        IOptions<SesEmailOptions> options,
        IAmazonSimpleEmailServiceV2 client,
        ILogger<SesEmailSender> logger)
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

        var fromAddress = message.From ?? opts.FromAddress;
        var fromDisplay = message.FromDisplayName ?? opts.FromDisplayName;
        // SES expects the From header as a single "Display Name <local@domain>" string when a name is present.
        var from = string.IsNullOrWhiteSpace(fromDisplay) ? fromAddress : $"{fromDisplay} <{fromAddress}>";

        var request = new SendEmailRequest
        {
            FromEmailAddress = from,
            Destination = new Destination { ToAddresses = new List<string> { message.To } },
            Content = new EmailContent
            {
                Simple = new Message
                {
                    Subject = new Content { Data = message.Subject, Charset = "UTF-8" },
                    Body = new Body
                    {
                        Html = new Content { Data = message.HtmlBody, Charset = "UTF-8" },
                        Text = new Content { Data = message.PlainTextBody, Charset = "UTF-8" },
                    },
                },
            },
        };

        if (!string.IsNullOrWhiteSpace(message.ReplyTo))
        {
            request.ReplyToAddresses = new List<string> { message.ReplyTo };
        }

        if (!string.IsNullOrEmpty(opts.ConfigurationSet))
        {
            request.ConfigurationSetName = opts.ConfigurationSet;
        }

        try
        {
            var response = await _client.SendEmailAsync(request, cancellationToken).ConfigureAwait(false);
            return !string.IsNullOrEmpty(response.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TapInAuth SES send failed to {To}", message.To);
            return false;
        }
    }
}

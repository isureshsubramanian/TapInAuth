using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TapInAuth.Delivery;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
// Aliased because TapInAuth.PhoneNumber (added in v1.0 for normalization) is a sibling namespace
// of TapInAuth.Sms.Twilio and would shadow Twilio.Types.PhoneNumber via enclosing-namespace lookup.
using TwilioPhoneNumber = Twilio.Types.PhoneNumber;

namespace TapInAuth.Sms.Twilio;

/// <summary>Twilio implementation of <see cref="ISmsSender"/>.</summary>
public sealed class TwilioSmsSender : ISmsSender
{
    private readonly IOptions<TwilioSmsOptions> _options;
    private readonly ILogger<TwilioSmsSender> _logger;
    private bool _initialized;
    private readonly object _initLock = new();

    /// <summary>Construct the sender.</summary>
    public TwilioSmsSender(IOptions<TwilioSmsOptions> options, ILogger<TwilioSmsSender> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var opts = _options.Value;

        EnsureInitialized(opts);

        if (string.IsNullOrWhiteSpace(opts.FromNumber) && string.IsNullOrWhiteSpace(opts.MessagingServiceSid))
        {
            throw new InvalidOperationException("TapInAuth.Sms.Twilio: configure either FromNumber or MessagingServiceSid.");
        }

        try
        {
            var createOptions = new CreateMessageOptions(new TwilioPhoneNumber(message.To))
            {
                Body = message.Body,
            };
            if (!string.IsNullOrWhiteSpace(opts.MessagingServiceSid))
            {
                createOptions.MessagingServiceSid = opts.MessagingServiceSid;
            }
            else
            {
                createOptions.From = new TwilioPhoneNumber(message.From ?? opts.FromNumber!);
            }

            var resource = await MessageResource.CreateAsync(createOptions).ConfigureAwait(false);
            // Twilio returns statuses like queued / sending / sent / delivered / undelivered / failed.
            // We treat anything except "failed" / "undelivered" as accepted-by-provider.
            var accepted = resource.Status != MessageResource.StatusEnum.Failed
                        && resource.Status != MessageResource.StatusEnum.Undelivered;
            if (!accepted)
            {
                _logger.LogWarning("TapInAuth Twilio: message to {To} returned status {Status} ({Code} {ErrorMessage})",
                    message.To, resource.Status, resource.ErrorCode, resource.ErrorMessage);
            }
            return accepted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TapInAuth Twilio: send failed to {To}", message.To);
            return false;
        }
    }

    private void EnsureInitialized(TwilioSmsOptions opts)
    {
        if (_initialized)
        {
            return;
        }
        lock (_initLock)
        {
            if (_initialized)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(opts.AccountSid) || string.IsNullOrWhiteSpace(opts.AuthToken))
            {
                throw new InvalidOperationException("TapInAuth.Sms.Twilio: AccountSid and AuthToken are required.");
            }
            TwilioClient.Init(opts.AccountSid, opts.AuthToken);
            _initialized = true;
        }
    }
}

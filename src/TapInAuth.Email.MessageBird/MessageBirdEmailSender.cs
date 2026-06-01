using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TapInAuth.Delivery;

namespace TapInAuth.Email.MessageBird;

/// <summary>
/// MessageBird (Bird) Channels API <see cref="IEmailSender"/> implementation.
/// </summary>
/// <remarks>
/// Bird's transactional email is exposed via the Channels API:
/// <c>POST /workspaces/{workspaceId}/channels/{channelId}/messages</c>. The body shape is
/// channel-specific — for email channels Bird expects a <c>receiver</c> with contact identifier,
/// a <c>body</c> with html / text variants, and metadata for subject. This implementation tracks
/// the published Bird Channels v1 contract; if Bird ships a typed .NET SDK we can swap to that.
/// </remarks>
public sealed class MessageBirdEmailSender : IEmailSender
{
    /// <summary>Named HttpClient registration key used by <c>AddMessageBirdEmail</c>.</summary>
    public const string HttpClientName = "TapInAuth.Email.MessageBird";

    private static readonly JsonSerializerOptions s_json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IOptions<MessageBirdEmailOptions> _options;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<MessageBirdEmailSender> _logger;

    /// <summary>Construct a MessageBird sender.</summary>
    public MessageBirdEmailSender(
        IOptions<MessageBirdEmailOptions> options,
        IHttpClientFactory httpFactory,
        ILogger<MessageBirdEmailSender> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _httpFactory = httpFactory ?? throw new ArgumentNullException(nameof(httpFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var opts = _options.Value;

        var fromAddress = message.From ?? opts.FromAddress;
        var fromDisplay = message.FromDisplayName ?? opts.FromDisplayName;

        var payload = new BirdMessageRequest
        {
            Receiver = new BirdReceiver
            {
                Contacts = new[]
                {
                    new BirdContact { Identifier = new BirdIdentifier { Key = "emailaddress", Value = message.To } },
                },
            },
            Body = new BirdBody
            {
                Type = "html",
                Html = new BirdHtmlBody { Text = message.HtmlBody },
            },
            Sender = new BirdSender
            {
                Contact = new BirdContact { Identifier = new BirdIdentifier { Key = "emailaddress", Value = fromAddress } },
                DisplayName = fromDisplay,
            },
            Meta = new BirdMeta { Subject = message.Subject, PlainText = message.PlainTextBody },
        };

        var url = $"{opts.ApiBaseUrl.TrimEnd('/')}/workspaces/{opts.WorkspaceId}/channels/{opts.ChannelId}/messages";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload, options: s_json),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("AccessKey", opts.AccessKey);

        if (message.Headers is { Count: > 0 })
        {
            foreach (var (k, v) in message.Headers)
            {
                request.Headers.TryAddWithoutValidation(k, v);
            }
        }

        // Resolve a fresh HttpClient per call from the factory. The factory recycles handlers internally,
        // so this is safe and avoids the stale-handler trap of capturing a single HttpClient in a singleton.
        var http = _httpFactory.CreateClient(HttpClientName);

        try
        {
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError(
                "TapInAuth MessageBird send to {To} failed: {Status} {Body}",
                message.To,
                (int)response.StatusCode,
                body);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TapInAuth MessageBird send failed to {To}", message.To);
            return false;
        }
    }

    // Request DTOs for the Bird Channels API. Kept internal — the public surface is just IEmailSender.
    private sealed class BirdMessageRequest
    {
        public BirdReceiver? Receiver { get; set; }
        public BirdBody? Body { get; set; }
        public BirdSender? Sender { get; set; }
        public BirdMeta? Meta { get; set; }
    }

    private sealed class BirdReceiver
    {
        public BirdContact[]? Contacts { get; set; }
    }

    private sealed class BirdContact
    {
        public BirdIdentifier? Identifier { get; set; }
    }

    private sealed class BirdIdentifier
    {
        public string? Key { get; set; }
        public string? Value { get; set; }
    }

    private sealed class BirdBody
    {
        public string? Type { get; set; }
        public BirdHtmlBody? Html { get; set; }
    }

    private sealed class BirdHtmlBody
    {
        public string? Text { get; set; }
    }

    private sealed class BirdSender
    {
        public BirdContact? Contact { get; set; }
        public string? DisplayName { get; set; }
    }

    private sealed class BirdMeta
    {
        public string? Subject { get; set; }
        public string? PlainText { get; set; }
    }
}

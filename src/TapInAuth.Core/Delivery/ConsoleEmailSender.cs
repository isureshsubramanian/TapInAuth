using Microsoft.Extensions.Logging;
using TapInAuth.Delivery;

namespace TapInAuth.Core.Delivery;

/// <summary>
/// Development-only <see cref="IEmailSender"/> that logs the message instead of sending it.
/// Use in samples and tests so you can see the magic link in the console without configuring SMTP.
/// </summary>
public sealed class ConsoleEmailSender(ILogger<ConsoleEmailSender> logger) : IEmailSender
{
    private readonly ILogger<ConsoleEmailSender> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        _logger.LogInformation(
            "TapInAuth ConsoleEmailSender → {To}\nSubject: {Subject}\n---\n{PlainText}\n---",
            message.To, message.Subject, message.PlainTextBody);
        return Task.FromResult(true);
    }
}

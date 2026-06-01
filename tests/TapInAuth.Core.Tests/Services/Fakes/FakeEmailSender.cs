using TapInAuth.Delivery;

namespace TapInAuth.Core.Tests.Services.Fakes;

/// <summary>Captures every email the service tries to send; tests inspect <see cref="Sent"/>.</summary>
public sealed class FakeEmailSender : IEmailSender
{
    /// <summary>Set to false to simulate a provider rejection (returns false from SendAsync).</summary>
    public bool ShouldSucceed { get; set; } = true;

    public List<EmailMessage> Sent { get; } = new();

    public Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        Sent.Add(message);
        return Task.FromResult(ShouldSucceed);
    }
}

using TapInAuth.Delivery;

namespace TapInAuth.Core.Tests.Services.Fakes;

/// <summary>Captures every SMS the service tries to send; tests inspect <see cref="Sent"/>.</summary>
public sealed class FakeSmsSender : ISmsSender
{
    /// <summary>Set to false to simulate a provider rejection (returns false from SendAsync).</summary>
    public bool ShouldSucceed { get; set; } = true;

    public List<SmsMessage> Sent { get; } = new();

    public Task<bool> SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        Sent.Add(message);
        return Task.FromResult(ShouldSucceed);
    }
}

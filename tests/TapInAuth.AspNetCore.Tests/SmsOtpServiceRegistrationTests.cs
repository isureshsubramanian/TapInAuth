using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TapInAuth.AspNetCore.DependencyInjection;
using TapInAuth.Core.Services;
using TapInAuth.Delivery;
using TapInAuth.Store.EntityFrameworkCore;
using TapInAuth.Store.EntityFrameworkCore.DependencyInjection;
using Xunit;

namespace TapInAuth.AspNetCore.Tests;

/// <summary>
/// Guards the SmsOtpService DI factory registration. When a host enables Methods.SmsOtp but
/// forgets to register an ISmsSender, the FIRST attempt to resolve SmsOtpService should throw a
/// clear actionable error pointing to AddTwilioSms — rather than a cryptic constructor failure.
/// </summary>
public class SmsOtpServiceRegistrationTests
{
    [Fact]
    public void Resolving_SmsOtpService_throws_actionable_error_when_ISmsSender_missing()
    {
        var services = BuildHost();
        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();

        Exception? caught = null;
        try { _ = scope.ServiceProvider.GetRequiredService<SmsOtpService>(); }
        catch (Exception ex) { caught = ex; }

        caught.Should().NotBeNull("factory should reject construction when ISmsSender is missing");

        // The DI container may surface our InvalidOperationException directly, or wrap it.
        // Walk the exception chain and assert one of them carries our error text.
        var messages = new List<string>();
        for (var e = caught; e is not null; e = e.InnerException)
        {
            messages.Add(e.Message);
        }
        var combined = string.Join(" | ", messages);
        combined.Should().Contain("ISmsSender", "the actionable error should mention the missing interface");
        combined.Should().Contain("AddTwilioSms", "the actionable error should point to the fix");
    }

    [Fact]
    public void Resolving_SmsOtpService_succeeds_when_ISmsSender_present()
    {
        var services = BuildHost();
        services.AddSingleton<ISmsSender, NoopSmsSender>();

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<SmsOtpService>();

        svc.Should().NotBeNull();
    }

    [Fact]
    public void SmsOtpService_is_registered_with_scoped_lifetime()
    {
        // The factory wrapper we use must register the service as Scoped (matching the other
        // OTP services) so it can resolve scoped EF Core stores via the same scope.
        var services = BuildHost();
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(SmsOtpService));

        descriptor.Should().NotBeNull("SmsOtpService should be registered by AddTapInAuth");
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    private static ServiceCollection BuildHost()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase("sms-reg-" + Guid.NewGuid()));
        services.AddTapInAuth(o =>
        {
            o.Methods = TapInAuthMethod.SmsOtp | TapInAuthMethod.MagicLink;
            o.Relying.Id = "localhost";
            o.Relying.Name = "Test";
        }).AddEfCoreStore<TestDbContext>();
        // MagicLinkService / EmailOtpService are unconditionally registered as scoped and need
        // an IEmailSender to resolve. Without one, even resolving SmsOtpService through the scope
        // would fail later (the scope resolves siblings during scope teardown). A no-op sender
        // satisfies the side dependencies so we can isolate the SMS-specific assertion.
        services.AddSingleton<IEmailSender, NoopEmailSender>();
        return services;
    }

    private sealed class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyTapInAuthConfiguration();
    }

    private sealed class NoopSmsSender : ISmsSender
    {
        public Task<bool> SendAsync(SmsMessage message, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class NoopEmailSender : IEmailSender
    {
        public Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}

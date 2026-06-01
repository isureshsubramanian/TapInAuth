# Reference: email providers

TapInAuth ships five email providers behind a single `IEmailSender` contract.
Pick one, install its NuGet package, and call its DI extension after `AddTapInAuth(...)`.

| Provider     | Package                       | Best for                                                 |
| ------------ | ----------------------------- | -------------------------------------------------------- |
| SMTP         | `TapInAuth.Email.Smtp`        | Existing relay (Postfix, Exchange, Mailgun-SMTP, Hermex) |
| SendGrid     | `TapInAuth.Email.SendGrid`    | High-volume marketing platforms with auth on the side    |
| Amazon SES   | `TapInAuth.Email.Ses`         | AWS-hosted apps using IAM-role credentials               |
| Postmark     | `TapInAuth.Email.Postmark`    | Pure transactional sends with strong deliverability      |
| MessageBird  | `TapInAuth.Email.MessageBird` | Bird-platform tenants (Channels API)                     |

Only one `IEmailSender` is registered per app — the last one wins. Pick the one that matches the host environment.

## Single contract

All providers implement:

```csharp
namespace TapInAuth.Delivery;

public interface IEmailSender
{
    Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string PlainTextBody,
    string? From = null,
    string? FromDisplayName = null,
    string? ReplyTo = null,
    IReadOnlyDictionary<string, string>? Headers = null);
```

The provider packages do not surface their SDK types — swapping providers is a one-line change in `Program.cs`.

## SMTP

```csharp
using TapInAuth.Email.Smtp.DependencyInjection;

builder.Services.AddTapInAuth(opts => opts.Methods = TapInAuthMethod.MagicLink | TapInAuthMethod.EmailOtp)
    .AddSmtpEmail(builder.Configuration.GetSection("TapInAuth:Smtp"));
```

```json
"TapInAuth": {
  "Smtp": {
    "Host": "smtp.example.com",
    "Port": 587,
    "Username": "apikey",
    "Password": "REDACTED",
    "UseStartTls": true,
    "FromAddress": "no-reply@example.com",
    "FromDisplayName": "Acme"
  }
}
```

## SendGrid

```csharp
using TapInAuth.Email.SendGrid.DependencyInjection;

builder.Services.AddTapInAuth(...)
    .AddSendGridEmail(builder.Configuration.GetSection("TapInAuth:SendGrid"));
```

```json
"TapInAuth": {
  "SendGrid": {
    "ApiKey": "SG.REDACTED",
    "FromAddress": "no-reply@example.com",
    "FromDisplayName": "Acme",
    "DisableClickTracking": true
  }
}
```

`DisableClickTracking` defaults to `true`. Leave it on — SendGrid's link-wrapper would otherwise rewrite the magic-link URL through a tracking redirect, and email pre-fetchers (anti-virus scanners, link previews) following that redirect would consume the single-use token before the human clicked it.

## Amazon SES

```csharp
using TapInAuth.Email.Ses.DependencyInjection;

builder.Services.AddTapInAuth(...)
    .AddSesEmail(builder.Configuration.GetSection("TapInAuth:Ses"));
```

```json
"TapInAuth": {
  "Ses": {
    "Region": "us-east-1",
    "FromAddress": "no-reply@example.com",
    "FromDisplayName": "Acme",
    "ConfigurationSet": "default"
  }
}
```

Omit `AccessKey` / `SecretKey` in production — the AWS SDK picks up IAM-role credentials automatically. Set them explicitly only for local development.

Use the optional `ConfigurationSet` to route through a dedicated IP pool, enable event publishing, or override the MAIL FROM domain. SES handles those concerns server-side.

If the host already calls `services.AddAWSService<IAmazonSimpleEmailServiceV2>()` (from `AWSSDK.Extensions.NETCore.Setup`), register your AWS service registration **after** `AddSesEmail` — the later registration wins.

## Postmark

```csharp
using TapInAuth.Email.Postmark.DependencyInjection;

builder.Services.AddTapInAuth(...)
    .AddPostmarkEmail(builder.Configuration.GetSection("TapInAuth:Postmark"));
```

```json
"TapInAuth": {
  "Postmark": {
    "ServerToken": "REDACTED",
    "FromAddress": "no-reply@example.com",
    "FromDisplayName": "Acme",
    "MessageStream": "outbound",
    "DisableClickTracking": true
  }
}
```

Use the **server token**, not the account token. The default `"outbound"` stream is transactional, which is what you want for auth — Postmark rejects auth emails routed through Broadcast streams.

## MessageBird (Bird)

```csharp
using TapInAuth.Email.MessageBird.DependencyInjection;

builder.Services.AddTapInAuth(...)
    .AddMessageBirdEmail(builder.Configuration.GetSection("TapInAuth:MessageBird"));
```

```json
"TapInAuth": {
  "MessageBird": {
    "AccessKey": "REDACTED",
    "WorkspaceId": "00000000-0000-0000-0000-000000000000",
    "ChannelId":   "00000000-0000-0000-0000-000000000000",
    "FromAddress": "no-reply@example.com",
    "FromDisplayName": "Acme"
  }
}
```

Bird's Channels API is namespaced by workspace and channel — get both UUIDs from the Bird dashboard. The `FromAddress` must be on the verified sending domain bound to the channel.

The MessageBird sender uses a named `IHttpClientFactory` client. Attach Polly to it directly when you want resilience policies:

```csharp
builder.Services.AddHttpClient(MessageBirdEmailSender.HttpClientName)
    .AddStandardResilienceHandler();
```

## Choosing a provider

A short decision tree:

- **Local development?** Hermex (in-process SMTP) via the SMTP provider — see [getting-started](getting-started.md).
- **Already on AWS with IAM roles?** SES — no secret to manage.
- **Want best-in-class deliverability for transactional only?** Postmark.
- **Already paying for SendGrid for marketing?** SendGrid.
- **Bird-platform tenant?** MessageBird.
- **Have a corporate SMTP relay?** SMTP.

## Inline configuration

Every provider also supports an `Action<TOptions>` overload for code-only setup (useful in tests):

```csharp
builder.Services.AddTapInAuth(...)
    .AddPostmarkEmail(o =>
    {
        o.ServerToken      = "test-token";
        o.FromAddress      = "no-reply@example.com";
        o.FromDisplayName  = "Test";
    });
```

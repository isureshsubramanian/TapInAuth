# Announcing TapInAuth — passwordless auth for ASP.NET Core, with the UI included

*Posted: launch day*

I'm releasing TapInAuth — an MIT-licensed, drop-in passwordless authentication library for ASP.NET Core and Blazor. Today's release: v0.5.0.

## Why another auth library

If you're shipping a .NET app today, you have options:

- **fido2-net-lib** gives you the WebAuthn protocol. You still write the ceremonies, the UI, the persistence, the recovery path.
- **ASP.NET Core Identity** gives you a great password story and a Blazor passkey template. Useful if you're starting fresh; tedious if you already have an Identity app and want to add passwordless to it.
- **Bitwarden Passwordless.dev** is hosted SaaS — fine if you're OK with that, not if you need self-hosted.

What I wanted didn't exist: a NuGet I could install, configure with four lines, and get a complete sign-in flow that looks like it was designed for my product. So I built it.

## What's in v0.5.0

Five passwordless methods, one contract. Mix any combination via a flags enum:

```csharp
options.Methods = TapInAuthMethod.Passkey
                | TapInAuthMethod.MagicLink
                | TapInAuthMethod.EmailOtp
                | TapInAuthMethod.SmsOtp
                | TapInAuthMethod.RecoveryCode;
```

The library wires the UI and endpoints to match the methods you enabled. Disable `SmsOtp` and the phone form vanishes from the sign-in page.

**Provider freedom out of the box.** Email: SMTP (MailKit), SendGrid, Postmark, Amazon SES (v2), MessageBird. SMS: Twilio. Bot defense: Cloudflare Turnstile, hCaptcha. Single `IEmailSender` / `ISmsSender` / `IRiskSignalProvider` contracts behind each.

**Two UIs.** Razor Pages (`TapInAuth.UI`) and Razor Components / Blazor Server (`TapInAuth.UI.Blazor`). Both ship as Razor Class Libraries with shared theme tokens. Your host references one, picks an accent color, drops in a logo.

**Multi-tenant from row 1.** Every store call carries a `TenantContext`. Per-tenant branding is read by the layout. Tenant-scoped filtered unique indexes in the EF Core store. The included SaaS sample demonstrates three tenants with three brands resolved by subdomain.

**Identity adapter.** If you already have an Identity app, swap the EF user store for the adapter and `UserManager<IdentityUser>` runs the user side; TapInAuth runs everything else.

**Built-in admin dashboard.** Audit events, credential management, rate-limit visibility — gated behind a configurable role.

## Security defaults that match OWASP without you thinking about it

- HMAC-SHA256 hashed tokens with a per-instance pepper. Raw tokens never persisted.
- Constant-time comparisons (`CryptographicOperations.FixedTimeEquals`) on every redemption.
- Single-use redemption — magic links and OTPs are atomically consumed on success.
- Per-identifier rate limits on both issuance and verification.
- Per-OTP attempt counters — exhausted attempts consume the code.
- No enumeration leak — unknown emails and phones return the same response shape as known ones.
- Structured audit log piped to a built-in admin dashboard.

## The four-line quickstart

```csharp
// Program.cs
builder.Services.AddTapInAuth(o =>
{
    o.Logo.Path    = "wwwroot/img/your-logo.svg";
    o.Theme.Accent = "#2563EB";
    o.Methods      = TapInAuthMethod.Passkey
                   | TapInAuthMethod.MagicLink
                   | TapInAuthMethod.EmailOtp
                   | TapInAuthMethod.RecoveryCode;
})
.AddEfCoreStore<AppDbContext>()
.AddSmtpEmail(builder.Configuration.GetSection("Smtp"));

app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();   // surfaces TapInAuth.UI's pages
app.MapTapInAuth();    // /auth/* endpoints
```

That's the entire host-app surface.

## What's NOT shipping in v0.5.0

I want to be honest about the rough edges:

- **OIDC bridge** — you can't yet *be* an identity provider for downstream apps. Planned for v0.9.
- **Phone-only signup** — phone is a secondary identifier in v1. You register with email, attach a phone via the account page, then SMS sign-in works.
- **Test suite** — 75-case baseline today, growing toward 150 before 1.0. PasskeyService end-to-end tests aren't there yet (the ceremony fixtures are nontrivial).
- **i18n / RTL** — English-only and LTR for v0.x.

## Roadmap to 1.0

- **v0.6** (next 4 weeks): comprehensive test suite, MessageBird & SNS SMS senders, OpenAPI metadata.
- **v0.9** (pre-1.0): OIDC bridge, admin dashboard polish, i18n.
- **1.0**: .NET Foundation incubation submission, production-grade docs site, conference talks.

## How to get it

```bash
dotnet add package TapInAuth.AspNetCore
dotnet add package TapInAuth.Store.EntityFrameworkCore
dotnet add package TapInAuth.UI
dotnet add package TapInAuth.Email.Smtp
```

Or browse the metapackage and provider sub-packages on [nuget.org/profiles/tapinauth](https://www.nuget.org/profiles/tapinauth).

GitHub: [github.com/tapinauth/tapinauth](https://github.com/tapinauth/tapinauth)
Docs: [tapinauth.io/docs](https://tapinauth.io/docs)

If you build something with it, I'd love to hear about it. Open an issue, star the repo, or grab me on social.

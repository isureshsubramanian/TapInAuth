# TapInAuth

> **Tap to sign in.** Drop-in passwordless authentication for ASP.NET Core and Blazor — passkeys, magic links, email OTP, and recovery codes — with an executive-grade UI that themes to your brand and a multi-tenant store baked in from day one.

[![CI](https://github.com/tapinauth/tapinauth/actions/workflows/ci.yml/badge.svg)](https://github.com/tapinauth/tapinauth/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/TapInAuth.svg)](https://www.nuget.org/packages/TapInAuth)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-blueviolet)](https://dotnet.microsoft.com)

## What you get

| Method | Status | Package |
|---|---|---|
| 🔑 **Passkeys** (WebAuthn / FIDO2) | ✅ shipping | `TapInAuth.Core` (wraps `Fido2.AspNet`) |
| 📧 **Magic link** (email) | ✅ shipping | `TapInAuth.Core` |
| 🔢 **Email OTP** (6-digit code) | ✅ shipping | `TapInAuth.Core` |
| 🔄 **Recovery codes** (single-use) | ✅ shipping | `TapInAuth.Core` |
| 📱 **SMS OTP** sign-in (phone as secondary identifier) | ✅ shipping | `TapInAuth.Core` + `TapInAuth.Sms.Twilio` |
| 🤖 **Bot-defense** widgets (Turnstile / hCaptcha) | ✅ shipping | `TapInAuth.Risk.Turnstile`, `TapInAuth.Risk.HCaptcha` |

Plus: a Razor Pages UI **and** a Razor Components (Blazor Server) UI you can ship as-is or restyle with CSS variables, a tenant-aware EF Core store, an ASP.NET Core Identity adapter for existing apps, a built-in admin dashboard with audit feed, **five email providers** (SMTP, SendGrid, Postmark, Amazon SES, MessageBird) behind a single `IEmailSender` contract, and an MIT license.

## 5-line quickstart

```csharp
// Program.cs
builder.Services
    .AddTapInAuth(o =>
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
app.MapRazorPages();          // surfaces TapInAuth.UI's sign-in / OTP / passkeys / recovery pages
app.MapTapInAuth();           // mounts /auth/* endpoints
```

That's the entire host-app surface. Sign-in page, magic-link landing page, OTP entry, passkey ceremony, recovery flow, admin dashboard — all in the library, all themed to your `--tap-accent`.

## Already on ASP.NET Core Identity?

Swap one builder call:

```csharp
builder.Services.AddTapInAuth(...)
    .AddEfCoreStore<AppDbContext>()       // still hosts magic-link, OTP, recovery, passkey tables
    .AddIdentityAdapter<IdentityUser>()   // user table → Identity's AspNetUsers
    .AddSmtpEmail(...);
```

No second user table. `UserManager<IdentityUser>` runs the user side; TapInAuth runs everything else. See [`samples/Identity.Sample`](samples/Identity.Sample) for the end-to-end demo.

## Multi-tenant SaaS?

Tenancy is built in from day one — every store call is tenant-scoped. Add a resolver, point it at your tenant catalog, optionally override per-tenant logo / accent / WebAuthn RP id:

```csharp
builder.Services.AddTapInAuth(...)
    .AddEfCoreStore<AppDbContext>()
    .AddTenantResolver<MySubdomainTenantResolver>()
    .AddSmtpEmail(...);
```

See [`samples/SaaS.MultiTenant`](samples/SaaS.MultiTenant) for the three-tenant demo with per-tenant logos, brand colors, and credential isolation.

## Why TapInAuth?

| | TapInAuth | fido2-net-lib | ASP.NET Core Identity passkeys | Bitwarden Passwordless.dev |
|---|---|---|---|---|
| Passkeys (WebAuthn) | ✅ | ✅ (protocol only) | ✅ (Blazor template only) | ✅ (SaaS, $3/user/mo) |
| Magic link | ✅ | ❌ | ❌ | ❌ |
| Email/SMS OTP | ✅ | ❌ | ❌ | ❌ |
| Recovery codes | ✅ | ❌ | ❌ | ❌ |
| Built-in UI, themable | ✅ | ❌ | template only | hosted widget |
| Admin dashboard + audit log | ✅ | ❌ | ❌ | ✅ |
| Multi-tenant from day one | ✅ | N/A | ❌ | ✅ |
| Identity adapter | ✅ | N/A | N/A | N/A |
| OSS, self-hosted, no SaaS | ✅ | ✅ | ✅ | open-core |

## Documentation

| | |
|---|---|
| [Getting started](docs/getting-started.md) | Add TapInAuth to a fresh or existing ASP.NET Core app |
| [Concepts: multi-tenancy](docs/concepts-multi-tenancy.md) | Tenant resolution, per-tenant branding, isolation guarantees |
| [Concepts: cookie handoff](docs/concepts-cookie-handoff.md) | How TapInAuth cooperates with `AddAuthentication().AddCookie()` |
| [How-to: ASP.NET Core Identity adapter](docs/howto-identity-adapter.md) | Plug into an existing `IdentityUser` setup |
| [How-to: passkeys](docs/howto-passkeys.md) | WebAuthn config, RP id, conditional UI |
| [How-to: recovery codes](docs/howto-recovery-codes.md) | Generation, redemption, UX patterns |
| [How-to: admin dashboard](docs/howto-admin-dashboard.md) | Granting the admin role, audit feed |
| [How-to: theming](docs/howto-theming.md) | Design tokens, logo handling, dark/light mode |
| [How-to: SMS sign-in](docs/howto-sms-signin.md) | Phone as secondary identifier, account-page management |
| [Reference: options](docs/reference-options.md) | Every `TapInAuthOptions` knob |
| [Reference: endpoints](docs/reference-endpoints.md) | Every HTTP endpoint mounted by `MapTapInAuth()` |
| [Reference: email providers](docs/reference-email-providers.md) | SMTP / SendGrid / SES / Postmark / MessageBird |
| **Deploy** | [Azure](docs/deployment/azure.md) · [AWS](docs/deployment/aws.md) · [Docker](docs/deployment/docker.md) · [Kubernetes](docs/deployment/kubernetes.md) · [IIS](docs/deployment/iis.md) |

## Repository layout

```
src/
  TapInAuth.Abstractions/               Interfaces, DTOs, options
  TapInAuth.Core/                       Auth engine (magic link, OTP, SMS OTP, passkeys, recovery, hashing, rate limit, audit)
  TapInAuth.AspNetCore/                 DI extensions, endpoint mapping, cookie handoff, admin policy
  TapInAuth.Identity/                   ASP.NET Core Identity adapter
  TapInAuth.Store.EntityFrameworkCore/  EF Core store (tenant-aware) + EF audit sink
  TapInAuth.Email.Smtp/                 MailKit-based IEmailSender
  TapInAuth.Email.SendGrid/             SendGrid IEmailSender
  TapInAuth.Email.Ses/                  Amazon SES (v2) IEmailSender
  TapInAuth.Email.Postmark/             Postmark IEmailSender
  TapInAuth.Email.MessageBird/          MessageBird (Bird) IEmailSender
  TapInAuth.Sms.Twilio/                 Twilio ISmsSender
  TapInAuth.Risk.Turnstile/             Cloudflare Turnstile bot-defense
  TapInAuth.Risk.HCaptcha/              hCaptcha bot-defense
  TapInAuth.UI/                         Razor Pages UI (RCL) — sign-in, OTP, passkeys, recovery, account, admin
  TapInAuth.UI.Blazor/                  Razor Components UI (Blazor Server / interactive)

samples/
  Mvc.Quickstart/                       Single-tenant MVC app with all methods
  Identity.Sample/                      ASP.NET Core Identity + TapInAuth coexistence
  SaaS.MultiTenant/                     SaaS demo: subdomain tenants, per-tenant logo + theme
  BlazorServer.Quickstart/              Blazor Server app using TapInAuth.UI.Blazor

tests/
  TapInAuth.Abstractions.Tests/
  TapInAuth.Core.Tests/
  TapInAuth.AspNetCore.Tests/
```

## Building locally

Prerequisites: .NET SDK **10.0.300+** (`global.json` enforces).

```bash
git clone https://github.com/tapinauth/tapinauth.git
cd tapinauth
dotnet restore
dotnet build -c Release
dotnet test  -c Release
dotnet run   --project samples/Mvc.Quickstart
```

The samples include [Hermex](https://github.com/sureshsubramanian/Hermex) — an in-process dev SMTP server with a browser inbox at `/hermex` — so you can sign in via magic-link or OTP without configuring real SMTP.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). All commits must be signed off (DCO — `git commit -s`).

## Security

See [SECURITY.md](SECURITY.md) for the threat model and how to report vulnerabilities.

## License

MIT — see [LICENSE](LICENSE). Trademark "TapInAuth" is held by Suresh Subramanian; the open-source license does not grant a trademark license.

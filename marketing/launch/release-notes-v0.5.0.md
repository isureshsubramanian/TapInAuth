# TapInAuth v0.5.0 — first public release

*The first release I'd let a friend ship to production.*

## What this release is

TapInAuth is an MIT-licensed, drop-in passwordless authentication library for ASP.NET Core and Blazor. v0.5.0 is the first public release — feature-complete for the five core passwordless methods, multi-tenant from day one, and ships its own UI so you never write another sign-in page.

## Highlights

- **Five passwordless methods** behind a single `Methods` flags enum: passkeys (WebAuthn), magic links, email OTP, SMS OTP, recovery codes.
- **Two UIs** ship as Razor Class Libraries: `TapInAuth.UI` (Razor Pages) and `TapInAuth.UI.Blazor` (Razor Components / Blazor Server). Both themed via CSS variables.
- **Five email providers**: SMTP (MailKit), SendGrid, Postmark, Amazon SES v2, MessageBird. Single `IEmailSender` contract — swap in one line.
- **Twilio SMS** out of the box. Phone is a secondary identifier — register with email first, attach phone via the account page.
- **Bot defense** via Cloudflare Turnstile and hCaptcha. Auto-renders the widget; gates magic-link / OTP / SMS issuance server-side.
- **Multi-tenant** from row 1. Every store call carries a `TenantContext`. Per-tenant branding (logo, accent, WebAuthn RP id). Tenant-scoped filtered unique indexes in the EF Core store.
- **ASP.NET Core Identity adapter** so you can keep `UserManager<IdentityUser>` and the existing `AspNetUsers` table.
- **Built-in admin dashboard** — audit feed, credential management, rate-limit visibility — gated behind a configurable role.
- **Cookie handoff** to the host's auth scheme. TapInAuth builds the `ClaimsPrincipal`; you decide what to do with it.

## Security defaults

- HMAC-SHA256 hashed tokens with a per-instance pepper. Raw tokens never persisted.
- `CryptographicOperations.FixedTimeEquals` on every redemption.
- Single-use redemption — magic links and OTPs are atomically consumed on success.
- Per-identifier rate limits on issuance AND verification.
- Per-OTP attempt counters — exhausted attempts consume the code.
- No enumeration leak — unknown emails and phones return the same response shape as known ones.
- Structured audit log piped to the built-in admin dashboard.

## Packages shipped

| Package                                 | Purpose                                          |
| --------------------------------------- | ------------------------------------------------ |
| `TapInAuth.Abstractions`                | Interfaces, DTOs, options                        |
| `TapInAuth.Core`                        | Magic link / OTP / SMS OTP / passkey / recovery services |
| `TapInAuth.AspNetCore`                  | DI extensions, endpoint mapping, cookie handoff  |
| `TapInAuth.Store.EntityFrameworkCore`   | EF Core store + audit sink                       |
| `TapInAuth.Identity`                    | ASP.NET Core Identity adapter                    |
| `TapInAuth.UI`                          | Razor Pages UI (RCL)                             |
| `TapInAuth.UI.Blazor`                   | Razor Components UI (RCL)                        |
| `TapInAuth.Email.Smtp`                  | MailKit SMTP sender                              |
| `TapInAuth.Email.SendGrid`              | SendGrid HTTP sender                             |
| `TapInAuth.Email.Postmark`              | Postmark sender                                  |
| `TapInAuth.Email.Ses`                   | Amazon SES v2 sender                             |
| `TapInAuth.Email.MessageBird`           | MessageBird (Bird) sender                        |
| `TapInAuth.Sms.Twilio`                  | Twilio SMS sender                                |
| `TapInAuth.Risk.Turnstile`              | Cloudflare Turnstile risk provider               |
| `TapInAuth.Risk.HCaptcha`               | hCaptcha risk provider                           |

## Samples

- `Mvc.Quickstart` — single-tenant MVC with all methods enabled, in-process Hermex dev SMTP.
- `Identity.Sample` — ASP.NET Core Identity coexisting with TapInAuth.
- `SaaS.MultiTenant` — three tenants with three brand colors, subdomain resolution, claim-vs-tenant guard.
- `BlazorServer.Quickstart` — Blazor Server using `TapInAuth.UI.Blazor`.

## Tested on

- .NET 10.0.x
- SQLite, SQL Server, PostgreSQL (via EF Core 10)
- Latest Chrome, Edge, Safari, Firefox (passkeys require WebAuthn-capable browsers)

## Known limitations / not in v0.5.0

- **No OIDC bridge.** You can't yet *be* an OAuth/OIDC identity provider for downstream apps. Planned for v0.9.
- **No phone-only signup.** Phone is a secondary identifier — register email first, attach phone via the account page, then SMS sign-in works.
- **Test suite is at 75 cases.** Comprehensive coverage of magic-link, email OTP, SMS OTP, recovery codes, phone normalization, and DI wiring. PasskeyService end-to-end tests, EF store integration tests, and risk-provider HttpMessageHandler tests are on the roadmap.
- **English-only, LTR.** i18n/RTL planned for v0.9.

## Roadmap

- **v0.6** (next 4 weeks): comprehensive test suite, MessageBird & SNS SMS senders, OpenAPI metadata.
- **v0.9** (pre-1.0): OIDC bridge, admin dashboard polish, i18n / RTL.
- **1.0**: .NET Foundation incubation submission, production-grade docs site, trademark + governance docs.

## Getting started

```csharp
// Program.cs
builder.Services.AddTapInAuth(o =>
{
    o.Methods = TapInAuthMethod.Passkey
              | TapInAuthMethod.MagicLink
              | TapInAuthMethod.EmailOtp;
    o.Theme.Accent = "#2563EB";
})
.AddEfCoreStore<AppDbContext>()
.AddSmtpEmail(builder.Configuration.GetSection("Smtp"));

app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapTapInAuth();
```

That's the entire host-app surface. Full walkthrough: [tapinauth.io/docs/getting-started](https://tapinauth.io/docs/getting-started).

## Thanks

To everyone who reviewed early drafts, broke the alphas, and pushed back on bad defaults. Bugs, ideas, and PRs all welcome at [github.com/tapinauth/tapinauth](https://github.com/tapinauth/tapinauth).

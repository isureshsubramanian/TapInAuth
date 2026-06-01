# NuGet package blurbs

Short descriptions for the `<Description>` element in each `.csproj`. NuGet search results truncate
around 120 characters, so each blurb is ≤120 chars in the headline; longer prose follows for the
package detail page.

---

## Metapackage (`TapInAuth`)

> Drop-in passwordless authentication for ASP.NET Core and Blazor — passkeys, magic links, OTP, SMS, recovery, multi-tenant.

## TapInAuth.Abstractions

> Interfaces, DTOs, and options for TapInAuth — passwordless auth for ASP.NET Core. Reference this if you're building a custom store or provider.

## TapInAuth.Core

> Auth engine for TapInAuth — magic-link, email/SMS OTP, passkey, and recovery-code services with HMAC-hashed tokens and rate limits.

## TapInAuth.AspNetCore

> DI extensions, endpoint mapping, cookie handoff, and admin policy for TapInAuth. Call AddTapInAuth() and MapTapInAuth() to wire the whole library.

## TapInAuth.Store.EntityFrameworkCore

> EF Core store for TapInAuth — tenant-aware user, credential, magic-link, OTP, recovery-code, and audit tables. Add to your existing DbContext.

## TapInAuth.Identity

> ASP.NET Core Identity adapter for TapInAuth. Reuse your AspNetUsers table — UserManager runs the user side, TapInAuth runs everything else.

## TapInAuth.UI

> Razor Pages UI for TapInAuth. Sign-in, magic-link, OTP, passkey, recovery, account, and admin pages, themed via CSS variables to your brand.

## TapInAuth.UI.Blazor

> Razor Components UI for TapInAuth (Blazor Server). Drop-in sign-in, OTP, passkey, recovery, and account pages with the same theme tokens as Razor Pages.

## TapInAuth.Email.Smtp

> MailKit-backed SMTP IEmailSender for TapInAuth. Pair with any SMTP relay (Postfix, Mailtrap, SendGrid SMTP, etc.).

## TapInAuth.Email.SendGrid

> SendGrid HTTP IEmailSender for TapInAuth. Click-tracking off by default — prefetchers can't burn single-use magic-link tokens.

## TapInAuth.Email.Postmark

> Postmark transactional IEmailSender for TapInAuth. Outbound stream by default; click-tracking off so magic-link tokens survive pre-fetchers.

## TapInAuth.Email.Ses

> Amazon SES (v2) IEmailSender for TapInAuth. Honors IAM-role credentials; supports SES configuration sets for IP-pool routing.

## TapInAuth.Email.MessageBird

> MessageBird (Bird) Channels IEmailSender for TapInAuth. HttpClient-based; bring your own retry policies via the named client.

## TapInAuth.Sms.Twilio

> Twilio ISmsSender for TapInAuth. Use MessagingServiceSid for short-code routing or FromNumber for direct-to-handset SMS.

## TapInAuth.Risk.Turnstile

> Cloudflare Turnstile bot-defense provider for TapInAuth. Gates magic-link and OTP issuance against the verified Turnstile token.

## TapInAuth.Risk.HCaptcha

> hCaptcha bot-defense provider for TapInAuth. Same contract as Turnstile — auto-renders the widget, verifies server-side.

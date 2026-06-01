# Hacker News — Show HN submission

HN rewards minimalism in the title and substance in the body. No marketing language.

---

## Title

`Show HN: TapInAuth – Passwordless auth for ASP.NET Core, with the UI included`

## URL

`https://github.com/tapinauth/tapinauth`

## Text (first comment)

I've shipped the same five auth components in every .NET app I've worked on — magic links, passkey ceremonies, email/SMS OTP, recovery codes, and the sign-in UI. Existing libraries give you the protocol or a template, but you still write the actual sign-in page. TapInAuth ships the page.

The whole host-app surface is four lines in Program.cs. You get a sign-in screen, OTP entry, passkey ceremony, recovery flow, account self-service, and an audit dashboard — themed by CSS variables to your accent color and logo. Razor Pages and Blazor variants ship in the package.

Design notes that might be interesting here:

- Passkeys via Fido2.AspNet — I wrap the ceremonies, persist credentials, and own the UI. I don't reimplement WebAuthn.
- Multi-tenant from row 1. Every store call carries a TenantContext. Per-tenant branding. Filtered unique indexes for tenant-scoped uniqueness.
- HMAC-SHA256 hashed tokens with a per-instance pepper. Constant-time compare. Single-use. Per-identifier rate limits on issue AND verify. Unknown identifiers return the same response shape as known ones — no enumeration oracle.
- Cookie handoff to the host's existing auth scheme rather than issuing its own session.
- ASP.NET Core Identity adapter — drop into an existing AspNetUsers table without a second user store.

Provider freedom: SMTP, SendGrid, Postmark, Amazon SES, MessageBird for email; Twilio for SMS; Cloudflare Turnstile and hCaptcha for bot defense. Single contract behind each.

It's v0.5.0, MIT, .NET 10. Roadmap to 1.0 includes OIDC bridge, .NET Foundation incubation, and a larger test suite past the 75-case baseline.

Would love feedback — especially on the multi-tenant primitives and the security defaults.

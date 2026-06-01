# /r/dotnet — Show HN-style launch post

Subreddit rules: no excessive self-promotion, technical depth wins. Lead with the code, not the marketing.

---

## Title

`I open-sourced TapInAuth — passwordless auth (passkeys, magic links, OTP, SMS, recovery) for ASP.NET Core and Blazor, with the UI included`

## Body

Hey r/dotnet,

I've been writing the same five things in every .NET project I've shipped — magic links, OTP, passkey ceremonies, recovery codes, and the sign-in UI that ties them together. So I packaged it as a NuGet library. MIT, .NET 10. Just released v0.5.0.

**The four-line setup:**

```csharp
builder.Services.AddTapInAuth(o =>
{
    o.Methods = TapInAuthMethod.Passkey
              | TapInAuthMethod.MagicLink
              | TapInAuthMethod.EmailOtp
              | TapInAuthMethod.RecoveryCode;
})
.AddEfCoreStore<AppDbContext>()
.AddSmtpEmail(builder.Configuration.GetSection("Smtp"));

app.MapRazorPages();   // surfaces TapInAuth.UI's sign-in / OTP / passkey / recovery / account / admin pages
app.MapTapInAuth();    // /auth/* endpoints
```

That's it. Sign-in page, magic-link landing, OTP entry, passkey ceremony, recovery flow, account self-service, audit dashboard — all in the library, themed via CSS variables to your accent color and logo.

**What's interesting technically:**

- Passkeys via `Fido2.AspNet` 4.0 — I don't roll my own protocol; just wrap the ceremonies and persist credentials.
- Multi-tenant from day one. Every store call carries a `TenantContext`. Tenant-aware filtered unique indexes in the EF Core store. Per-tenant branding read by the layout. Three-tenant SaaS sample with subdomain resolution.
- HMAC-SHA256 token hashing with per-instance pepper. `CryptographicOperations.FixedTimeEquals` on every compare. Single-use redemption. Rate limits on both issue and verify. Unknown identifiers return the same response shape as known ones (no enumeration oracle).
- Identity adapter — drop the EF user store, point `UserManager<IdentityUser>` at the existing AspNetUsers table. No second user table.
- Cookie handoff (the host's cookie scheme), not a custom session. TapInAuth builds the `ClaimsPrincipal`; you decide what to do with it.
- 9 NuGet packages, layered: Abstractions / Core / AspNetCore / UI / UI.Blazor / Store.EntityFrameworkCore / Identity adapter / email + SMS + risk providers.

**Provider freedom (you pick):**

- Email: SMTP (MailKit), SendGrid, Postmark, Amazon SES (v2), MessageBird
- SMS: Twilio
- Bot defense: Cloudflare Turnstile, hCaptcha

Single `IEmailSender` / `ISmsSender` / `IRiskSignalProvider` contracts behind each. Swap in one line.

**Samples included:**

- `Mvc.Quickstart` — single-tenant MVC, all methods enabled, in-process Hermex dev SMTP so you can sign in without configuring a real provider
- `SaaS.MultiTenant` — three tenants, three brand colors, subdomain resolution
- `Identity.Sample` — TapInAuth coexisting with `IdentityUser`
- `BlazorServer.Quickstart` — Razor Components version of the UI

**Links:**

- Code: github.com/tapinauth/tapinauth
- NuGet: nuget.org/packages/TapInAuth
- Docs: tapinauth.io/docs

**Where it's NOT yet:**

- It's v0.5.0, not 1.0. Test suite has ~75 cases today, growing. PasskeyService end-to-end tests are still on the list.
- No OIDC bridge yet (planned for v0.9).
- No phone-only signup. Phone is a secondary identifier in v1 — register email first, attach phone via the account page, then SMS sign-in works.

Would love feedback, especially from anyone shipping passkeys today. Happy to answer questions about the design.

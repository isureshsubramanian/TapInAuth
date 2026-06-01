# LinkedIn — launch post

Audience: CTOs, engineering managers, senior .NET devs.
Tone: confident, value-oriented, no emoji stack at the top.

---

Today I'm open-sourcing TapInAuth — a drop-in passwordless authentication library for ASP.NET Core and Blazor.

**The problem.** Every team I've worked with rebuilds the same five things to ship modern auth on .NET: passkey ceremonies, magic links, OTP delivery, recovery codes, and the sign-in UI that holds it all together. The protocol libraries (Fido2.AspNet, etc.) give you the bits. ASP.NET Core Identity gives you a template. Neither gives you a product.

**The library.** Four lines of Program.cs gets you the complete UX:

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
```

The sign-in page, the OTP entry, the passkey ceremony, the recovery flow, and the admin dashboard all ship in the package. Razor Pages and Blazor UIs included. Themed via CSS variables — your logo, your accent, your brand. Already on ASP.NET Core Identity? One line swaps the user store; you keep `UserManager<IdentityUser>`.

**Multi-tenant by default.** Every store call is tenant-scoped. The included SaaS sample shows three tenants with per-tenant logos, accent colors, and isolated credentials — same code, three brands.

**Security defaults that match OWASP without you thinking about it.** HMAC-SHA256 hashed tokens with a per-instance pepper, constant-time comparisons, single-use redemption, per-identifier rate limits on both issuance and verification, no enumeration leak on unknown emails or phones, structured audit log piped to a built-in admin dashboard.

**Provider freedom.** Email: SMTP, SendGrid, Postmark, Amazon SES, MessageBird. SMS: Twilio. Risk: Cloudflare Turnstile and hCaptcha. Single contracts behind each — swap providers in one line.

**Today's release: v0.5.0.** MIT licensed. .NET 10. The roadmap to 1.0 includes an OIDC bridge, .NET Foundation incubation, additional SMS providers, and a comprehensive test suite past the 75-test baseline that ships today.

If you're shipping auth on .NET, or if you're tired of writing the sign-in page for the seventeenth time, I'd love a star and your feedback.

🔗 GitHub: github.com/tapinauth/tapinauth
🔗 NuGet: nuget.org/packages/TapInAuth
🔗 Docs: tapinauth.io/docs

#dotnet #aspnetcore #blazor #passwordless #passkeys #webauthn #opensource

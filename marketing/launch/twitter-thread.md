# Twitter / X — launch thread

Audience: .NET devs, indie hackers, SaaS founders.
Hashtags kept to the last tweet only. URLs use `tapinauth.io` as the canonical link.

---

**1/7**
After years of writing the same five things for every ASP.NET Core app — magic links, passwordless OTP, passkey ceremonies, the recovery flow, the dark/light sign-in UI — I packaged it.

TapInAuth: passwordless auth for ASP.NET Core and Blazor.

Four lines of Program.cs.

→ tapinauth.io

---

**2/7**
What's in the box:

🔑 Passkeys (WebAuthn / FIDO2)
📧 Magic links
🔢 Email + SMS OTP
🔄 Recovery codes
🤖 Bot defense (Turnstile / hCaptcha)
🎨 Razor Pages + Blazor UIs, themed to your brand
🏢 Multi-tenant from row 1

MIT licensed. Self-hosted.

---

**3/7**
The bit other libraries skip:

The UI.

You don't write the sign-in page. You don't draw the OTP boxes. You don't style the "we sent you a link" landing. The library ships an executive-grade UI as a Razor Class Library — themed by CSS variables.

Drop in your logo, pick an accent color, done.

---

**4/7**
Already on ASP.NET Core Identity?

```csharp
builder.Services.AddTapInAuth(...)
    .AddEfCoreStore<AppDbContext>()
    .AddIdentityAdapter<IdentityUser>()
    .AddSmtpEmail(...);
```

No second user table. UserManager runs the user side; TapInAuth runs everything else.

---

**5/7**
Provider freedom, not lock-in:

📧 Email: SMTP · SendGrid · Postmark · Amazon SES · MessageBird
📱 SMS: Twilio
🛡️ Risk: Cloudflare Turnstile · hCaptcha

Single `IEmailSender` / `ISmsSender` / `IRiskSignalProvider` contracts. Swap providers in one line.

---

**6/7**
The security defaults match the OWASP cheat sheet without you having to think:

• HMAC-SHA256 hashed tokens with per-instance pepper
• Constant-time compare on every verify
• Single-use redemption
• Per-identifier rate limits on issue AND verify
• No enumeration leak on unknown identifiers
• Structured audit log + built-in admin dashboard

---

**7/7**
It's MIT. It runs on .NET 10. Today's release: v0.5.0.

Roadmap to 1.0: OIDC bridge, .NET Foundation incubation, more SMS providers.

⭐ github.com/tapinauth/tapinauth
📦 nuget.org/packages/TapInAuth
📖 tapinauth.io/docs

#dotnet #aspnetcore #blazor #passkeys #passwordless

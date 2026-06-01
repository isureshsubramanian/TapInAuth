# Getting started

TapInAuth in five minutes.

## Prerequisites

- .NET SDK **10.0.300** or newer (a `global.json` in the repo pins this).
- An ASP.NET Core app — fresh `dotnet new web` works fine.
- An EF Core DbContext you control (any provider — SQLite, SQL Server, PostgreSQL, MySQL).
- For passkeys: HTTPS or `localhost` (browsers refuse WebAuthn on plain HTTP).

## 1. Add the packages

```bash
dotnet add package TapInAuth
dotnet add package TapInAuth.AspNetCore
dotnet add package TapInAuth.Store.EntityFrameworkCore
dotnet add package TapInAuth.Email.Smtp
dotnet add package TapInAuth.UI
```

For an existing ASP.NET Core Identity app add `TapInAuth.Identity` and skip the EF Core user-store registration.

## 2. Add TapInAuth's tables to your DbContext

```csharp
using TapInAuth.Store.EntityFrameworkCore;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyTapInAuthConfiguration();
    }
}
```

That adds `TapInAuthUsers`, `TapInAuthMagicLinkTokens`, `TapInAuthOtpCodes`, `TapInAuthRecoveryCodes`, `TapInAuthCredentials`, and (if you opt in) `TapInAuthAuditEvents`.

## 3. Wire DI in `Program.cs`

```csharp
using Microsoft.AspNetCore.Authentication.Cookies;
using TapInAuth;
using TapInAuth.AspNetCore.DependencyInjection;
using TapInAuth.Email.Smtp.DependencyInjection;
using TapInAuth.Store.EntityFrameworkCore.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/auth/sign-in";
        o.LogoutPath = "/auth/sign-out";
    });
builder.Services.AddAuthorization();

builder.Services.AddTapInAuth(o =>
    {
        o.Methods      = TapInAuthMethod.Passkey
                       | TapInAuthMethod.MagicLink
                       | TapInAuthMethod.EmailOtp
                       | TapInAuthMethod.RecoveryCode;
        o.Theme.Accent = "#2563EB";
        o.Logo.Path    = "wwwroot/img/your-logo.svg";
        o.FromEmail    = "no-reply@yourdomain.com";
        o.FromDisplayName = "Your App";

        // For passkeys: the RP id should be your apex domain.
        o.Relying.Id   = "yourdomain.com";
        o.Relying.Name = "Your App";
        o.Relying.AllowedOrigins.Add("https://yourdomain.com");
    })
    .AddEfCoreStore<AppDbContext>()
    .AddSmtpEmail(builder.Configuration.GetSection("Smtp"));
    // Swap SMTP for SendGrid / Postmark / Amazon SES / MessageBird —
    // see [reference-email-providers.md](reference-email-providers.md).

builder.Services.AddRazorPages();

var app = builder.Build();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapTapInAuth();
app.Run();
```

## 4. Configure SMTP

`appsettings.json`:

```json
{
  "ConnectionStrings": { "Default": "Data Source=app.db" },
  "Smtp": {
    "Host": "smtp.sendgrid.net",
    "Port": 587,
    "Username": "apikey",
    "Password": "{secret}",
    "UseStartTls": true,
    "FromAddress": "no-reply@yourdomain.com",
    "FromDisplayName": "Your App"
  }
}
```

Keep `Password` in user-secrets / a secret store — never in source control. See the [deployment guides](deployment/) for cloud-specific patterns.

## 5. Run

```bash
dotnet ef database update   # if you use migrations, otherwise EnsureCreated() works for a first try
dotnet run
```

Visit your app, get redirected to `/auth/sign-in`, choose how to sign in.

## What you got

- `/auth/sign-in` — themed sign-in page with passkey, magic-link, OTP options.
- `/auth/sent` — "we sent you a link" landing.
- `/auth/otp` — code-entry page.
- `/auth/recovery` — recovery-code redemption.
- `/auth/passkeys` (your own route) — passkey management; see [passkeys how-to](howto-passkeys.md).
- `/auth/admin` — built-in admin dashboard for users with the configured admin role.

## Next steps

- [Concepts: multi-tenancy](concepts-multi-tenancy.md) — if this is a SaaS, tenancy is on by default and you just need a resolver.
- [Concepts: cookie handoff](concepts-cookie-handoff.md) — TapInAuth never owns your session cookie. Here's how that contract works.
- [Reference: options](reference-options.md) — every knob.

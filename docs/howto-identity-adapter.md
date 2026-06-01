# How-to: ASP.NET Core Identity adapter

If your app already uses `IdentityUser` + `UserManager` (any flavor — `AddIdentity`, `AddIdentityCore`, custom `IdentityDbContext` subclass), don't create a second user table. The `TapInAuth.Identity` package routes user lookups to your existing one.

## Install

```bash
dotnet add package TapInAuth.Identity
```

## DI

Swap **one** builder call: replace the default user-store registration with the Identity adapter.

```diff
 builder.Services.AddTapInAuth(...)
     .AddEfCoreStore<AppDbContext>()
-    // (default) uses TapInAuth's own TapInAuthUsers table
+    .AddIdentityAdapter<IdentityUser>()
     .AddSmtpEmail(...);
```

`.AddEfCoreStore` still hosts the magic-link, OTP, recovery, passkey, and audit tables. `.AddIdentityAdapter<TUser>` redirects the **user** side to your `AspNetUsers` table via `UserManager<TUser>`.

## What's mapped

| TapInAuth concept | Identity equivalent |
|---|---|
| `TapInAuthUser.Id` (Guid) | `IdentityUser.Id` (string — must parse to Guid) |
| `TapInAuthUser.Email` | `IdentityUser.Email` |
| `TapInAuthUser.EmailVerified` | `IdentityUser.EmailConfirmed` |
| Creating a new user (on magic-link / OTP sign-in for unknown email, if `AllowSignUp = true`) | `UserManager.CreateAsync(new TUser { Id = Guid…, Email = …, UserName = … })` |
| `SetEmailVerifiedAsync` | `GenerateEmailConfirmationTokenAsync` + `ConfirmEmailAsync` (so Identity's pipeline runs) |

## Requirements & limits

- **`IdentityUser.Id` must be a Guid string** (the default for `IdentityUser`). If you use `IdentityUser<int>` or a custom key type, hand-write a `ITapInAuthUserStore` instead — it's ~50 lines.
- **Single-tenant by default.** Every Identity user gets stamped with `TenantContext.DefaultTenantId`. For a multi-tenant Identity app, either add a `TenantId` column to a custom `ApplicationUser : IdentityUser` and write a custom adapter, or use a per-tenant `DbContext`.
- **Identity owns the password rules.** TapInAuth doesn't use passwords, but Identity's `Password.RequiredLength` etc. apply when `UserManager.CreateAsync` runs without a password (the sample relaxes them — see below).

## Sample `Program.cs`

```csharp
builder.Services
    .AddIdentityCore<IdentityUser>(o =>
    {
        // No password is ever set via TapInAuth — relax the policy so CreateAsync(new TUser()) succeeds.
        o.Password.RequireDigit = false;
        o.Password.RequireLowercase = false;
        o.Password.RequireUppercase = false;
        o.Password.RequireNonAlphanumeric = false;
        o.Password.RequiredLength = 1;
        o.User.RequireUniqueEmail = true;
        o.SignIn.RequireConfirmedEmail = false;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o => { o.LoginPath = "/auth/sign-in"; o.LogoutPath = "/auth/sign-out"; });
builder.Services.AddAuthorization();

builder.Services.AddTapInAuth(o => { /* ... */ })
    .AddEfCoreStore<AppDbContext>()
    .AddIdentityAdapter<IdentityUser>()
    .AddSmtpEmail(...);
```

See [`samples/Identity.Sample`](../samples/Identity.Sample) for the full app.

## Admin role with Identity

Identity has roles built in — use them directly. No claims transformation needed.

```csharp
// One-off provisioning:
await userManager.AddToRoleAsync(user, "TapInAuthAdmin");
```

The admin role name comes from `TapInAuthOptions.Security.AdminRole` (defaults to `"TapInAuthAdmin"`).

## When NOT to use the adapter

- You want TapInAuth's user store to live in a separate database from Identity's.
- Your Identity user table has business-critical columns that you don't want TapInAuth to touch (the adapter only reads `Id`, `Email`, `UserName`, `EmailConfirmed` — it doesn't write anything else, but the principle stands).
- You're starting greenfield and don't need Identity at all — use `.AddEfCoreStore<AppDbContext>()` alone.

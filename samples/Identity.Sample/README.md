# TapInAuth — Identity Sample

Shows TapInAuth coexisting with ASP.NET Core Identity. Identity owns the user table (`AspNetUsers`); TapInAuth plugs in via `.AddIdentityAdapter<IdentityUser>()` and routes all user lookups through `UserManager<IdentityUser>`. Magic-link tokens, OTP codes, and passkey credentials still live in TapInAuth's own EF Core tables, joined by the Guid-string Identity user id.

## Run

```bash
dotnet run --project samples/Identity.Sample
```

Open:

- https://localhost:5201 — the app (redirects to sign-in)
- https://localhost:5201/hermex — Hermex dev inbox (your magic-link / OTP emails land here)

## Try

1. Visit https://localhost:5201, get redirected to `/auth/sign-in`.
2. Type any email and hit **Email me a sign-in link**.
3. Open `/hermex`, click the link inside the captured email.
4. You're signed in — Identity provisioned an `AspNetUsers` row for you on first use; TapInAuth handed off the cookie to ASP.NET Core's default cookie scheme.
5. Add a passkey from `/passkeys`. Sign out, then click **Sign in with a passkey** on the sign-in page.

## What's different from `Mvc.Quickstart`

| | `Mvc.Quickstart` | `Identity.Sample` |
|---|---|---|
| User table | TapInAuth's own `TapInAuthUsers` | Identity's `AspNetUsers` |
| User store registration | `.AddEfCoreStore<AppDbContext>()` | `.AddEfCoreStore<AppDbContext>() + .AddIdentityAdapter<IdentityUser>()` |
| Use this when | Greenfield app | You already use `IdentityUser`/`UserManager` and don't want a second user table |

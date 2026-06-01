# TapInAuth — MVC Quickstart

Single-tenant ASP.NET Core MVC app demonstrating TapInAuth with **magic link** and **email OTP** sign-in, an EF Core SQLite store, and the **Hermex** in-process dev SMTP server for inspecting sent emails.

## Run it

```bash
dotnet run --project samples/Mvc.Quickstart
```

Then open:

- https://localhost:5001 — the app (will redirect to the sign-in page)
- https://localhost:5001/hermex — the Hermex inbox where the sent magic-link / OTP emails appear

## Try it

1. Open https://localhost:5001 — you're redirected to `/auth/sign-in`.
2. Enter any email (no real address required — Hermex captures it locally) and press **Continue**.
3. Open https://localhost:5001/hermex — you'll see the styled magic-link email.
4. Click the **Sign in** button inside the email — you're signed in to the app.
5. Try **Email me a code instead** for the OTP flow.

No SMTP credentials, no Docker, no external mail account.

## What's wired up

- `AddTapInAuth(...)` configures the methods and theme.
- `.AddEfCoreStore<AppDbContext>()` plugs the EF Core store into the host's own DbContext.
- `.AddSmtpEmail(...)` points TapInAuth's MailKit-based SMTP sender at Hermex on `localhost:2525`.
- `AddMail4Dev(...)` + `UseMail4Dev()` run Hermex in-process — SMTP listener on 2525, dashboard at `/hermex`.
- `app.MapTapInAuth()` mounts the `/auth/*` endpoints (sign-in, verify, OTP request/verify, sign-out).
- `app.MapRazorPages()` hosts the TapInAuth UI pages.

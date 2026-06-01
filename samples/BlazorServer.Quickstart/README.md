# TapInAuth — Blazor Server Quickstart

Blazor Server host using **`TapInAuth.UI.Blazor`** — sign-in / OTP / magic-link / recovery as interactive Razor Components instead of Razor Pages.

## Architecture

- **UI**: `.razor` components from `TapInAuth.UI.Blazor` (SignIn, MagicLinkSent, Otp, Recovery) plus the host's own `Home` page.
- **Auth flow**: still POSTs to TapInAuth's HTTP endpoints (`/auth/magic-link`, `/auth/otp/verify`, etc.) — that's how cookies get set (Blazor's SignalR connection can't set cookies).
- **Storage**: the same `TapInAuth.Store.EntityFrameworkCore` package on a SQLite DB.
- **Email**: Hermex in-process dev SMTP at `localhost:2525` + browser inbox at `/hermex`.

## Run

```bash
dotnet run --project samples/BlazorServer.Quickstart
```

Then:

- https://localhost:5301 — the Blazor app (redirects to sign-in)
- https://localhost:5301/hermex — Hermex inbox

## When to choose this over the Razor Pages UI

| Scenario | UI to choose |
|---|---|
| MVC / Razor Pages host | `TapInAuth.UI` (Razor Pages) |
| Blazor Server host | `TapInAuth.UI.Blazor` (this sample) |
| Blazor Web App with SSR-rendered auth pages | `TapInAuth.UI.Blazor` |
| Blazor WASM app | `TapInAuth.UI` for the auth pages (they're HTTP routes anyway), pure components for the rest |

Both packages render the same executive design tokens — your end-users won't notice the difference.

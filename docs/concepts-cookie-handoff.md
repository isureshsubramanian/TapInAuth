# Concepts: cookie handoff

TapInAuth verifies the user. **The host application owns the session.** TapInAuth never issues cookies of its own — it produces a `ClaimsPrincipal` and hands it to the host's existing authentication scheme. That's "cookie handoff."

## Why

Almost every adopter already has:

```csharp
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o => { /* your cookie name, lifetime, SameSite, signing keys */ });
```

If TapInAuth introduced its own scheme on top, you'd have two cookies fighting over the response, two sets of claims to merge, two policies to coordinate. Instead, TapInAuth produces the principal and lets your existing pipeline do the rest. Cookie name, lifetime, sliding expiration, `SameSite`, the data-protection keys — all unchanged.

## The contract

`IAuthenticationHandoff` is a one-method interface in `TapInAuth.Abstractions`:

```csharp
public interface IAuthenticationHandoff
{
    Task SignInAsync(AuthenticationHandoffContext context,
                     ClaimsPrincipal principal,
                     CancellationToken cancellationToken = default);
    Task SignOutAsync(AuthenticationHandoffContext context,
                      CancellationToken cancellationToken = default);
}
```

`AuthenticationHandoffContext` carries the current `HttpContext`, the resolved tenant, the auth method used (passkey / magic-link / OTP / recovery), and an optional return URL.

## Default implementation

`TapInAuth.AspNetCore` registers `CookieAuthenticationHandoff` by default. It calls:

```csharp
await httpContext.SignInAsync(
    CookieAuthenticationDefaults.AuthenticationScheme,
    principal,
    new AuthenticationProperties { IsPersistent = ..., RedirectUri = ... });
```

That writes the `.AspNetCore.Cookies` cookie using your `AddCookie(...)` config. No extra step from you.

## Claims TapInAuth produces

On the principal:

| Claim type | Value |
|---|---|
| `ClaimTypes.NameIdentifier` | TapInAuth user id (Guid) |
| `ClaimTypes.Email` | the user's email |
| `ClaimTypes.Name` | display name (falls back to email) |
| `tapinauth:tenant` | tenant id the user signed into |
| `tapinauth:amr` | method used: `passkey` / `magiclink` / `emailotp` / `smsotp` |
| `tapinauth:auth_time` | unix seconds at sign-in |
| `tapinauth:email_verified` | `"true"` / `"false"` |

The constants live in `TapInAuth.Claims.TapInAuthClaimTypes`.

## Custom handoff (JWT, IdentityServer, etc.)

If you mint your own session tokens — JWTs for an SPA, OAuth tokens via IdentityServer, etc. — implement `IAuthenticationHandoff` and register it:

```csharp
public class JwtHandoff : IAuthenticationHandoff
{
    public Task SignInAsync(AuthenticationHandoffContext ctx, ClaimsPrincipal p, CancellationToken ct)
    {
        var http = (HttpContext)ctx.HttpContext;
        var jwt = _tokens.Mint(p);
        http.Response.Cookies.Append("session", jwt, ...);
        return Task.CompletedTask;
    }
    public Task SignOutAsync(AuthenticationHandoffContext ctx, CancellationToken ct)
    {
        var http = (HttpContext)ctx.HttpContext;
        http.Response.Cookies.Delete("session");
        return Task.CompletedTask;
    }
}

builder.Services.AddTapInAuth(...).AddAuthenticationHandoff<JwtHandoff>();
```

## What TapInAuth does NOT touch

- Your cookie name or path.
- Your cookie lifetime / sliding expiration.
- Your data-protection keys (used to sign/encrypt the cookie).
- Your `OnTicketReceived` / `OnSigningIn` / other cookie events.
- Your claims transformations.
- Your authorization policies (TapInAuth registers one — `"TapInAuth.Admin"` — and otherwise stays out of the way).

If you're already running `AddAuthentication().AddCookie(...)` it keeps doing the same thing it always did. TapInAuth just gives it a principal to bake into the cookie.

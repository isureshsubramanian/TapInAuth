# How-to: admin dashboard

Built-in admin pages at `/auth/admin` for diagnosing sign-in activity and reviewing audit events. Gated behind a single role.

## Enable

The admin policy is registered automatically by `AddTapInAuth(...)`. To populate the audit feed, opt in to the persistent EF audit sink:

```csharp
builder.Services.AddTapInAuth(...)
    .AddEfCoreStore<AppDbContext>()
    .AddEfCoreAuditSink<AppDbContext>();   // ← persists audit events to TapInAuthAuditEvents
```

Without this, audit events go to `ILogger` only (still useful in your log aggregator, just not browsable in the admin UI).

## Grant the admin role

Whichever role name `TapInAuthOptions.Security.AdminRole` resolves to (default `"TapInAuthAdmin"`), that's the gate. Two common patterns:

### Pattern 1 — ASP.NET Core Identity (recommended for Identity apps)

```csharp
await userManager.AddToRoleAsync(user, "TapInAuthAdmin");
```

Identity automatically projects role claims onto the principal. Done.

### Pattern 2 — claims transformation (for non-Identity apps)

```csharp
public sealed class AdminRoleClaimsTransformation : IClaimsTransformation
{
    private readonly IOptions<MyOptions> _opts;
    public AdminRoleClaimsTransformation(IOptions<MyOptions> opts) => _opts = opts;

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity id || !id.IsAuthenticated) return Task.FromResult(principal);
        var email = principal.FindFirst(ClaimTypes.Email)?.Value;
        if (_opts.Value.AdminEmails.Contains(email, StringComparer.OrdinalIgnoreCase))
        {
            id.AddClaim(new Claim(ClaimTypes.Role, "TapInAuthAdmin"));
        }
        return Task.FromResult(principal);
    }
}

builder.Services.AddScoped<IClaimsTransformation, AdminRoleClaimsTransformation>();
```

See [`samples/Mvc.Quickstart/Auth/AdminRoleClaimsTransformation.cs`](../samples/Mvc.Quickstart/Auth/AdminRoleClaimsTransformation.cs) for a full sample.

## Pages

| Route | What |
|---|---|
| `/auth/admin` | Overview — counters (events in last 24h, successful/failed sign-ins, rate-limit triggers) + 10 most recent events |
| `/auth/admin/audit` | Full audit feed, newest first, up to 200 entries |

Both are tenant-scoped: an admin signed in to Acme only sees Acme's data.

## Custom role name

```csharp
builder.Services.AddTapInAuth(o => o.Security.AdminRole = "Acme.GlobalAdmin");
```

The policy reads the role name from options at evaluation time — no rebuild needed if you bind from config.

## What's NOT in the dashboard yet

The admin page is intentionally minimal in 0.x:
- No user search / cross-user view.
- No "force revoke this user's session" (sign-out a remote browser).
- No per-tenant configuration editor.

Post-1.0 stretch goals. For now, the audit feed gives security buyers what they need to see during procurement: "yes, every sign-in is recorded, here's a browsable feed scoped to my tenant."

## Audit event types

```csharp
public enum AuditEventType
{
    UserCreated,
    SignInStarted, SignInCompleted, SignInFailed,
    MagicLinkIssued, MagicLinkRedeemed, MagicLinkExpired, MagicLinkInvalid,
    OtpIssued, OtpVerified, OtpInvalid, OtpAttemptsExceeded,
    CredentialRegistered, CredentialAsserted, CredentialRevoked,
    RateLimitTriggered, SignedOut,
}
```

Each event captures `Timestamp`, `TenantId`, `UserId?`, `Email?`, `IpAddress?`, `UserAgent?`, `Detail?`, `Success`. The IP and UA fields are currently optional in the built-in sinks — extend the audit pipeline if you need them populated from `HttpContext`.

## Custom audit sink

Implement `IAuditSink` (write side) and optionally `IAuditQuery` (read side, so the admin dashboard can render your data). Register both:

```csharp
public sealed class MySplunkAuditSink : IAuditSink, IAuditQuery { /* ... */ }

builder.Services.Replace(ServiceDescriptor.Scoped<IAuditSink, MySplunkAuditSink>());
builder.Services.AddScoped<IAuditQuery, MySplunkAuditSink>();
```

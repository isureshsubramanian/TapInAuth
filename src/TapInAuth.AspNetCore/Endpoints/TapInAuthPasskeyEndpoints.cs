using System.Text.Json;
using Fido2NetLib;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using TapInAuth;
using TapInAuth.Core.Services;
using TapInAuth.Handoff;
using TapInAuth.Options;
using TapInAuth.Stores;
using TapInAuth.Tenancy;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// WebAuthn / passkey endpoints. Mounted under <c>{BasePath}/passkey</c> by <c>MapTapInAuth</c>.
/// Ceremony state (the challenge + the original options) is held in an HMAC-signed, short-TTL cookie
/// via ASP.NET Core Data Protection — no server-side session storage required.
/// </summary>
public static class TapInAuthPasskeyEndpoints
{
    private const string RegisterCookieName = "tapin.auth.passkey.register";
    private const string AssertCookieName   = "tapin.auth.passkey.assert";
    private const string ProtectionPurpose  = "TapInAuth.Passkey.CeremonyState.v1";

    /// <summary>Map the passkey routes onto the given group (typically <c>{BasePath}/passkey</c>).</summary>
    public static IEndpointConventionBuilder MapTapInAuthPasskeyEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        // POST /auth/passkey/register/options  — produce CredentialCreateOptions for navigator.credentials.create()
        group.MapPost("/register/options", RegisterOptionsAsync).WithName("TapInAuth.Passkey.RegisterOptions");

        // POST /auth/passkey/register          — verify attestation and store the credential
        group.MapPost("/register",         RegisterAsync).WithName("TapInAuth.Passkey.Register");

        // POST /auth/passkey/assert/options    — produce AssertionOptions for navigator.credentials.get()
        group.MapPost("/assert/options",   AssertOptionsAsync).WithName("TapInAuth.Passkey.AssertOptions");

        // POST /auth/passkey/assert            — verify assertion and sign the user in via the host's auth scheme
        group.MapPost("/assert",           AssertAsync).WithName("TapInAuth.Passkey.Assert");

        // GET  /auth/passkey/me                — list the signed-in user's passkeys (id, deviceName, createdAt, lastUsedAt)
        group.MapGet ("/me",               ListMyAsync).WithName("TapInAuth.Passkey.ListMine");

        // POST /auth/passkey/{id}/revoke       — remove one of the signed-in user's passkeys
        group.MapPost("/{id:guid}/revoke", RevokeAsync).WithName("TapInAuth.Passkey.Revoke");

        return group;
    }

    // ──────────────────────── list / revoke ────────────────────────

    private static async Task<IResult> ListMyAsync(
        HttpContext http,
        ICredentialStore credentialStore,
        ITenantResolver tenantResolver,
        ITapInAuthUserStore userStore)
    {
        if (http.User.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }
        var email = http.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                 ?? http.User.Identity.Name;
        if (string.IsNullOrWhiteSpace(email))
        {
            return Results.BadRequest(new { error = "no_email_claim" });
        }
        var tenant = (await tenantResolver.ResolveAsync(http.RequestAborted).ConfigureAwait(false)) ?? TenantContext.Default;
        var user = await userStore.FindByEmailAsync(tenant, email, http.RequestAborted).ConfigureAwait(false);
        if (user is null)
        {
            return Results.Ok(Array.Empty<object>());
        }
        var creds = await credentialStore.ListForUserAsync(tenant, user.Id, http.RequestAborted).ConfigureAwait(false);
        return Results.Ok(creds.Select(c => new
        {
            id = c.Id,
            deviceName = c.DeviceName,
            createdAt = c.CreatedAt,
            lastUsedAt = c.LastUsedAt,
        }));
    }

    private static async Task<IResult> RevokeAsync(
        Guid id,
        HttpContext http,
        ICredentialStore credentialStore,
        ITenantResolver tenantResolver,
        ITapInAuthUserStore userStore)
    {
        if (http.User.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }
        var email = http.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                 ?? http.User.Identity.Name;
        if (string.IsNullOrWhiteSpace(email))
        {
            return Results.BadRequest(new { error = "no_email_claim" });
        }
        var tenant = (await tenantResolver.ResolveAsync(http.RequestAborted).ConfigureAwait(false)) ?? TenantContext.Default;
        var user = await userStore.FindByEmailAsync(tenant, email, http.RequestAborted).ConfigureAwait(false);
        if (user is null)
        {
            return Results.NotFound();
        }
        // Verify the credential belongs to the signed-in user before deleting (defence-in-depth).
        var owned = await credentialStore.ListForUserAsync(tenant, user.Id, http.RequestAborted).ConfigureAwait(false);
        if (!owned.Any(c => c.Id == id))
        {
            return Results.NotFound();
        }
        await credentialStore.DeleteAsync(tenant, id, http.RequestAborted).ConfigureAwait(false);
        return Results.Ok(new { revoked = id });
    }

    // ──────────────────────── registration ────────────────────────

    private static async Task<IResult> RegisterOptionsAsync(
        HttpContext http,
        PasskeyService passkeys,
        ITenantResolver tenantResolver,
        ITapInAuthUserStore userStore,
        IDataProtectionProvider dp)
    {
        if (http.User.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }
        var emailClaim = http.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                      ?? http.User.Identity.Name;
        if (string.IsNullOrWhiteSpace(emailClaim))
        {
            return Results.BadRequest(new { error = "no_email_claim" });
        }
        var tenant = (await tenantResolver.ResolveAsync(http.RequestAborted).ConfigureAwait(false)) ?? TenantContext.Default;
        var user = await userStore.FindByEmailAsync(tenant, emailClaim, http.RequestAborted).ConfigureAwait(false);
        if (user is null)
        {
            return Results.BadRequest(new { error = "user_not_found" });
        }

        var options = passkeys.StartRegistration(tenant, user);
        WriteCeremonyCookie(http, dp, RegisterCookieName, PasskeyService.Serialize(options));
        return Results.Content(options.ToJson(), "application/json");
    }

    private static async Task<IResult> RegisterAsync(
        HttpContext http,
        PasskeyService passkeys,
        ITenantResolver tenantResolver,
        ITapInAuthUserStore userStore,
        IDataProtectionProvider dp)
    {
        if (http.User.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }
        var emailClaim = http.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                      ?? http.User.Identity.Name;
        if (string.IsNullOrWhiteSpace(emailClaim))
        {
            return Results.BadRequest(new { error = "no_email_claim" });
        }
        var tenant = (await tenantResolver.ResolveAsync(http.RequestAborted).ConfigureAwait(false)) ?? TenantContext.Default;
        var user = await userStore.FindByEmailAsync(tenant, emailClaim, http.RequestAborted).ConfigureAwait(false);
        if (user is null)
        {
            return Results.BadRequest(new { error = "user_not_found" });
        }

        var originalJson = ReadCeremonyCookie(http, dp, RegisterCookieName);
        if (originalJson is null)
        {
            return Results.BadRequest(new { error = "ceremony_expired" });
        }
        var originalOptions = PasskeyService.DeserializeCreateOptions(originalJson);

        var raw = await JsonSerializer.DeserializeAsync<AuthenticatorAttestationRawResponse>(
            http.Request.Body, cancellationToken: http.RequestAborted).ConfigureAwait(false);
        if (raw is null)
        {
            return Results.BadRequest(new { error = "invalid_payload" });
        }

        string? deviceName = http.Request.Query["deviceName"].ToString();

        var credential = await passkeys.CompleteRegistrationAsync(tenant, user, raw, originalOptions, deviceName, http.RequestAborted).ConfigureAwait(false);
        ClearCookie(http, RegisterCookieName);
        return credential is null
            ? Results.BadRequest(new { error = "verification_failed" })
            : Results.Ok(new { id = credential.Id, deviceName = credential.DeviceName, createdAt = credential.CreatedAt });
    }

    // ──────────────────────── assertion (sign-in) ────────────────────────

    private static async Task<IResult> AssertOptionsAsync(
        HttpContext http,
        PasskeyService passkeys,
        ITenantResolver tenantResolver,
        IDataProtectionProvider dp)
    {
        var tenant = (await tenantResolver.ResolveAsync(http.RequestAborted).ConfigureAwait(false)) ?? TenantContext.Default;
        var options = passkeys.StartAssertion(tenant);
        WriteCeremonyCookie(http, dp, AssertCookieName, PasskeyService.Serialize(options));
        return Results.Content(options.ToJson(), "application/json");
    }

    private static async Task<IResult> AssertAsync(
        HttpContext http,
        PasskeyService passkeys,
        ITenantResolver tenantResolver,
        IAuthenticationHandoff handoff,
        IDataProtectionProvider dp,
        IOptions<TapInAuthOptions> options)
    {
        var tenant = (await tenantResolver.ResolveAsync(http.RequestAborted).ConfigureAwait(false)) ?? TenantContext.Default;

        var originalJson = ReadCeremonyCookie(http, dp, AssertCookieName);
        if (originalJson is null)
        {
            return Results.BadRequest(new { error = "ceremony_expired" });
        }
        var originalOptions = PasskeyService.DeserializeAssertionOptions(originalJson);

        var raw = await JsonSerializer.DeserializeAsync<AuthenticatorAssertionRawResponse>(
            http.Request.Body, cancellationToken: http.RequestAborted).ConfigureAwait(false);
        if (raw is null)
        {
            return Results.BadRequest(new { error = "invalid_payload" });
        }

        var user = await passkeys.CompleteAssertionAsync(tenant, raw, originalOptions, http.RequestAborted).ConfigureAwait(false);
        ClearCookie(http, AssertCookieName);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var principal = passkeys.BuildPrincipal(tenant, user);
        await handoff.SignInAsync(
            new AuthenticationHandoffContext(http, tenant, TapInAuthMethod.Passkey, IsPersistent: false),
            principal,
            http.RequestAborted).ConfigureAwait(false);

        return Results.Ok(new { redirect = options.Value.Routes.DefaultReturnUrl });
    }

    // ──────────────────────── cookie helpers ────────────────────────

    private static void WriteCeremonyCookie(HttpContext http, IDataProtectionProvider dp, string name, string payload)
    {
        var protector = dp.CreateProtector(ProtectionPurpose);
        var protectedValue = protector.Protect(payload);
        http.Response.Cookies.Append(name, protectedValue, new CookieOptions
        {
            HttpOnly = true,
            Secure = http.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddMinutes(5),
            IsEssential = true,
            Path = "/auth",
        });
    }

    private static string? ReadCeremonyCookie(HttpContext http, IDataProtectionProvider dp, string name)
    {
        if (!http.Request.Cookies.TryGetValue(name, out var raw) || string.IsNullOrEmpty(raw))
        {
            return null;
        }
        try
        {
            return dp.CreateProtector(ProtectionPurpose).Unprotect(raw);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }

    private static void ClearCookie(HttpContext http, string name)
    {
        http.Response.Cookies.Delete(name, new CookieOptions { Path = "/auth" });
    }
}

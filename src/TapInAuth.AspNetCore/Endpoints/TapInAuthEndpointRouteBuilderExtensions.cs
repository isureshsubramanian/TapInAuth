using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TapInAuth;
using TapInAuth.Core.Services;
using TapInAuth.Handoff;
using TapInAuth.Options;
using TapInAuth.Risk;
using TapInAuth.Stores;
using TapInAuth.Tenancy;

// Placed in Microsoft.AspNetCore.Builder so MapTapInAuth() is auto-discovered via the
// Web SDK's implicit usings (same pattern as MapRazorPages, MapGet, etc.). Hosts don't
// need a separate `using TapInAuth.AspNetCore.Endpoints;`.
namespace Microsoft.AspNetCore.Builder;

/// <summary>Maps the TapInAuth HTTP endpoints under the configured base path.</summary>
public static class TapInAuthEndpointRouteBuilderExtensions
{
    /// <summary>Mount TapInAuth endpoints (and UI Razor Pages if installed) on the route builder.</summary>
    public static IEndpointConventionBuilder MapTapInAuth(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var opts = endpoints.ServiceProvider.GetRequiredService<IOptions<TapInAuthOptions>>().Value;
        var basePath = opts.Routes.BasePath.TrimEnd('/');

        var group = endpoints.MapGroup(basePath).WithTags("TapInAuth");

        // POST /auth/magic-link  { email, returnUrl }
        group.MapPost("/magic-link", IssueMagicLinkAsync)
             .WithName("TapInAuth.IssueMagicLink");

        // GET  /auth/verify?id=<tokenId>&t=<rawToken>
        group.MapGet("/verify", VerifyMagicLinkAsync)
             .WithName("TapInAuth.VerifyMagicLink");

        // POST /auth/otp/request { email }
        group.MapPost("/otp/request", RequestOtpAsync)
             .WithName("TapInAuth.RequestOtp");

        // POST /auth/otp/verify  { email, code, returnUrl }
        group.MapPost("/otp/verify", VerifyOtpAsync)
             .WithName("TapInAuth.VerifyOtp");

        // SMS-OTP endpoints — only mounted if SmsOtp method is enabled. The SmsOtpService also
        // requires an ISmsSender (TapInAuth.Sms.Twilio / future TapInAuth.Sms.MessageBird) to be
        // registered; if it isn't, the endpoint will throw at request time with a clear DI error.
        if ((opts.Methods & TapInAuthMethod.SmsOtp) != 0)
        {
            // POST /auth/sms/request { phone }
            group.MapPost("/sms/request", RequestSmsOtpAsync)
                 .WithName("TapInAuth.RequestSmsOtp");

            // POST /auth/sms/verify  { phone, code, returnUrl }
            group.MapPost("/sms/verify", VerifySmsOtpAsync)
                 .WithName("TapInAuth.VerifySmsOtp");

            // Account-page phone management — require an authenticated session. These are NOT part of
            // the sign-in flow; they let an already-signed-in user attach / detach / verify a phone.
            // POST /auth/account/phone/set    { phone }  → stores normalized phone, clears verified
            // POST /auth/account/phone/clear  { }        → removes phone, clears verified
            // POST /auth/account/phone/verify { code }   → marks phone verified (consumes the OTP)
            group.MapPost("/account/phone/set",    SetAccountPhoneAsync   ).WithName("TapInAuth.Account.SetPhone");
            group.MapPost("/account/phone/clear",  ClearAccountPhoneAsync ).WithName("TapInAuth.Account.ClearPhone");
        }

        // POST /auth/sign-out  — standard form-post sign-out
        group.MapPost("/sign-out", SignOutAsync)
             .WithName("TapInAuth.SignOut");
        // GET  /auth/sign-out  — convenience link-based sign-out for simple sample HTML.
        // For real apps, prefer the POST form to keep sign-out CSRF-safe.
        group.MapGet ("/sign-out", SignOutAsync)
             .WithName("TapInAuth.SignOutGet");

        // Passkey (WebAuthn) endpoints — only mounted if the Passkey method is enabled.
        if ((opts.Methods & TapInAuthMethod.Passkey) != 0)
        {
            group.MapGroup("/passkey").MapTapInAuthPasskeyEndpoints();
        }

        // Recovery-code endpoints — only mounted if the RecoveryCode method is enabled.
        if ((opts.Methods & TapInAuthMethod.RecoveryCode) != 0)
        {
            group.MapPost("/recovery/redeem",     RedeemRecoveryAsync).WithName("TapInAuth.Recovery.Redeem");
            group.MapPost("/recovery/regenerate", RegenerateRecoveryAsync).WithName("TapInAuth.Recovery.Regenerate");
            group.MapGet ("/recovery/count",      RecoveryCountAsync).WithName("TapInAuth.Recovery.Count");
        }

        return group;
    }

    // ──────────────────────── recovery codes ────────────────────────

    private static async Task<IResult> RedeemRecoveryAsync(
        HttpContext http,
        RecoveryCodeService recovery,
        ITenantResolver tenantResolver,
        IAuthenticationHandoff handoff,
        IOptions<TapInAuthOptions> options)
    {
        var form = await ReadFormAsync(http).ConfigureAwait(false);
        var email = form.TryGetValue("email", out var e) ? e.ToString() : null;
        var code  = form.TryGetValue("code",  out var c) ? c.ToString() : null;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
        {
            return Results.BadRequest(new { error = "missing_fields" });
        }

        var tenant = (await tenantResolver.ResolveAsync(http.RequestAborted).ConfigureAwait(false)) ?? TenantContext.Default;
        var user = await recovery.RedeemAsync(tenant, email!, code!, http.RequestAborted).ConfigureAwait(false);
        if (user is null)
        {
            var basePath = options.Value.Routes.BasePath.TrimEnd('/');
            return Results.Redirect(WithTenant($"{basePath}/recovery?error=invalid&email={Uri.EscapeDataString(email!)}", tenant));
        }

        var principal = recovery.BuildPrincipal(tenant, user);
        await handoff.SignInAsync(new AuthenticationHandoffContext(http, tenant, TapInAuthMethod.RecoveryCode), principal, http.RequestAborted).ConfigureAwait(false);
        return Results.Redirect(options.Value.Routes.DefaultReturnUrl);
    }

    private static async Task<IResult> RegenerateRecoveryAsync(
        HttpContext http,
        RecoveryCodeService recovery,
        ITenantResolver tenantResolver,
        ITapInAuthUserStore userStore)
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
        var codes = await recovery.RegenerateAsync(tenant, user.Id, http.RequestAborted).ConfigureAwait(false);
        return Results.Ok(new { codes });
    }

    private static async Task<IResult> RecoveryCountAsync(
        HttpContext http,
        RecoveryCodeService recovery,
        ITenantResolver tenantResolver,
        ITapInAuthUserStore userStore)
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
            return Results.Ok(new { remaining = 0 });
        }
        var remaining = await recovery.CountRemainingAsync(tenant, user.Id, http.RequestAborted).ConfigureAwait(false);
        return Results.Ok(new { remaining });
    }

    private static async Task<IResult> IssueMagicLinkAsync(
        HttpContext http,
        MagicLinkService magicLink,
        ITenantResolver tenantResolver,
        IOptions<TapInAuthOptions> options)
    {
        var form = await ReadFormAsync(http).ConfigureAwait(false);
        var email = form.TryGetValue("email", out var e) ? e.ToString() : null;
        if (string.IsNullOrWhiteSpace(email))
        {
            return Results.BadRequest(new { error = "email_required" });
        }
        var returnUrl = form.TryGetValue("returnUrl", out var r) ? r.ToString() : null;

        var tenant = (await tenantResolver.ResolveAsync(http.RequestAborted).ConfigureAwait(false)) ?? TenantContext.Default;

        // Bot-defense gate — runs only when both an IRiskSignalProvider and IRiskWidgetDescriptor are registered.
        var risk = await CheckRiskAsync(http, tenant, email!, form, options).ConfigureAwait(false);
        if (risk is not null) { return risk; }

        var opts = options.Value;
        var origin = $"{http.Request.Scheme}://{http.Request.Host}";
        // Include the issuing tenant on the verification URL so the redirect target lands back in the
        // right tenant context (matters for SaaS hosts with a tenant-aware ITenantResolver — without
        // this, the verify request resolves to the default tenant and can't find the stored token).
        // For single-tenant apps (tenant == "default") we omit the param to keep the URL clean.
        var tenantQuery = tenant.Id == TenantContext.DefaultTenantId ? "" : $"&tenant={Uri.EscapeDataString(tenant.Id)}";
        var template = $"{origin}{opts.Routes.BasePath.TrimEnd('/')}{opts.Routes.Verify}?id={{tokenId}}&t={{token}}{tenantQuery}";

        var outcome = await magicLink.IssueAsync(tenant, email!, template, returnUrl, http.RequestAborted).ConfigureAwait(false);

        var sentPath = WithTenant($"{opts.Routes.BasePath.TrimEnd('/')}{opts.Routes.MagicLinkSent}?email={Uri.EscapeDataString(email!)}", tenant);
        return outcome switch
        {
            MagicLinkIssueResult.Issued         => Results.Redirect(sentPath),
            MagicLinkIssueResult.RateLimited    => Results.Redirect(sentPath + "&rate=1"),
            MagicLinkIssueResult.DeliveryFailed => Results.Redirect(sentPath + "&err=delivery"),
            _ => Results.StatusCode(500),
        };
    }

    private static async Task<IResult> VerifyMagicLinkAsync(
        HttpContext http,
        MagicLinkService magicLink,
        ITenantResolver tenantResolver,
        IAuthenticationHandoff handoff,
        IOptions<TapInAuthOptions> options)
    {
        var tokenIdStr = http.Request.Query["id"].ToString();
        var rawToken = http.Request.Query["t"].ToString();
        if (string.IsNullOrWhiteSpace(tokenIdStr) || string.IsNullOrWhiteSpace(rawToken) || !Guid.TryParse(tokenIdStr, out var tokenId))
        {
            return Results.BadRequest(new { error = "invalid_request" });
        }

        var tenant = (await tenantResolver.ResolveAsync(http.RequestAborted).ConfigureAwait(false)) ?? TenantContext.Default;
        var result = await magicLink.RedeemAsync(tenant, tokenId, rawToken, http.RequestAborted).ConfigureAwait(false);
        return await CompleteSignInAsync(http, handoff, options, tenant, result).ConfigureAwait(false);
    }

    private static async Task<IResult> RequestOtpAsync(
        HttpContext http,
        EmailOtpService otp,
        ITenantResolver tenantResolver,
        IOptions<TapInAuthOptions> options)
    {
        var form = await ReadFormAsync(http).ConfigureAwait(false);
        var email = form.TryGetValue("email", out var e) ? e.ToString() : null;
        if (string.IsNullOrWhiteSpace(email))
        {
            return Results.BadRequest(new { error = "email_required" });
        }
        var tenant = (await tenantResolver.ResolveAsync(http.RequestAborted).ConfigureAwait(false)) ?? TenantContext.Default;

        var risk = await CheckRiskAsync(http, tenant, email!, form, options).ConfigureAwait(false);
        if (risk is not null) { return risk; }

        await otp.IssueAsync(tenant, email!, http.RequestAborted).ConfigureAwait(false);
        var opts = options.Value;
        var otpPath = WithTenant($"{opts.Routes.BasePath.TrimEnd('/')}{opts.Routes.Otp}?email={Uri.EscapeDataString(email!)}", tenant);
        return Results.Redirect(otpPath);
    }

    private static async Task<IResult> VerifyOtpAsync(
        HttpContext http,
        EmailOtpService otp,
        ITenantResolver tenantResolver,
        IAuthenticationHandoff handoff,
        IOptions<TapInAuthOptions> options)
    {
        var form = await ReadFormAsync(http).ConfigureAwait(false);
        var email = form.TryGetValue("email", out var e) ? e.ToString() : null;
        var code = form.TryGetValue("code", out var c) ? c.ToString() : null;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
        {
            return Results.BadRequest(new { error = "missing_fields" });
        }
        var tenant = (await tenantResolver.ResolveAsync(http.RequestAborted).ConfigureAwait(false)) ?? TenantContext.Default;
        var result = await otp.VerifyAsync(tenant, email!, code!, http.RequestAborted).ConfigureAwait(false);
        return await CompleteSignInAsync(http, handoff, options, tenant, result).ConfigureAwait(false);
    }

    private static async Task<IResult> RequestSmsOtpAsync(
        HttpContext http,
        SmsOtpService sms,
        ITenantResolver tenantResolver,
        IOptions<TapInAuthOptions> options)
    {
        var form = await ReadFormAsync(http).ConfigureAwait(false);
        var phone = form.TryGetValue("phone", out var p) ? p.ToString() : null;
        if (string.IsNullOrWhiteSpace(phone))
        {
            return Results.BadRequest(new { error = "phone_required" });
        }
        var tenant = (await tenantResolver.ResolveAsync(http.RequestAborted).ConfigureAwait(false)) ?? TenantContext.Default;

        // Bot-defense gate — same as email/magic-link issuance.
        var risk = await CheckRiskAsync(http, tenant, phone!, form, options).ConfigureAwait(false);
        if (risk is not null) { return risk; }

        // Service silently drops unknown / unregistered phones, so success on the redirect doesn't
        // confirm the phone exists. The OTP entry page renders the same "code sent" copy in either case.
        await sms.IssueAsync(tenant, phone!, http.RequestAborted).ConfigureAwait(false);
        var opts = options.Value;
        var smsPath = WithTenant($"{opts.Routes.BasePath.TrimEnd('/')}{opts.Routes.SmsOtp}?phone={Uri.EscapeDataString(phone!)}", tenant);
        return Results.Redirect(smsPath);
    }

    private static async Task<IResult> VerifySmsOtpAsync(
        HttpContext http,
        SmsOtpService sms,
        ITenantResolver tenantResolver,
        IAuthenticationHandoff handoff,
        IOptions<TapInAuthOptions> options)
    {
        var form = await ReadFormAsync(http).ConfigureAwait(false);
        var phone = form.TryGetValue("phone", out var p) ? p.ToString() : null;
        var code  = form.TryGetValue("code",  out var c) ? c.ToString() : null;
        if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(code))
        {
            return Results.BadRequest(new { error = "missing_fields" });
        }
        var tenant = (await tenantResolver.ResolveAsync(http.RequestAborted).ConfigureAwait(false)) ?? TenantContext.Default;
        var result = await sms.VerifyAsync(tenant, phone!, code!, http.RequestAborted).ConfigureAwait(false);
        return await CompleteSignInAsync(http, handoff, options, tenant, result).ConfigureAwait(false);
    }

    private static async Task<IResult> SetAccountPhoneAsync(
        HttpContext http,
        ITapInAuthUserStore userStore,
        SmsOtpService sms,
        ITenantResolver tenantResolver,
        IOptions<TapInAuthOptions> options)
    {
        if (http.User.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }
        var form = await ReadFormAsync(http).ConfigureAwait(false);
        var phone = form.TryGetValue("phone", out var p) ? p.ToString() : null;
        if (string.IsNullOrWhiteSpace(phone))
        {
            return Results.BadRequest(new { error = "phone_required" });
        }
        if (!TapInAuth.PhoneNumber.TryNormalize(phone!, out _))
        {
            return Results.Redirect(AccountPath(options.Value, await ResolveAsync(tenantResolver, http).ConfigureAwait(false)) + "?error=invalid_phone");
        }

        var emailClaim = http.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                      ?? http.User.Identity.Name;
        if (string.IsNullOrWhiteSpace(emailClaim))
        {
            return Results.BadRequest(new { error = "no_email_claim" });
        }
        var tenant = await ResolveAsync(tenantResolver, http).ConfigureAwait(false);
        var user = await userStore.FindByEmailAsync(tenant, emailClaim, http.RequestAborted).ConfigureAwait(false);
        if (user is null)
        {
            return Results.BadRequest(new { error = "user_not_found" });
        }

        // Uniqueness pre-check: don't let two users in the same tenant claim the same phone.
        // FindByPhoneAsync uses the same normalization as SetPhoneAsync, so the comparison is canonical.
        var existing = await userStore.FindByPhoneAsync(tenant, phone!, http.RequestAborted).ConfigureAwait(false);
        if (existing is not null && existing.Id != user.Id)
        {
            // Don't echo back which phone was taken — that's an enumeration oracle.
            return Results.Redirect(AccountPath(options.Value, tenant) + "?error=phone_in_use");
        }

        try
        {
            await userStore.SetPhoneAsync(tenant, user.Id, phone, http.RequestAborted).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
            return Results.Redirect(AccountPath(options.Value, tenant) + "?error=invalid_phone");
        }

        // Issue an SMS OTP straight away so the user can verify in one continuous flow.
        await sms.IssueAsync(tenant, phone!, http.RequestAborted).ConfigureAwait(false);
        var verifyPath = WithTenant($"{options.Value.Routes.BasePath.TrimEnd('/')}{options.Value.Routes.SmsOtp}?phone={Uri.EscapeDataString(phone!)}", tenant);
        return Results.Redirect(verifyPath);
    }

    private static async Task<IResult> ClearAccountPhoneAsync(
        HttpContext http,
        ITapInAuthUserStore userStore,
        ITenantResolver tenantResolver,
        IOptions<TapInAuthOptions> options)
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
        var tenant = await ResolveAsync(tenantResolver, http).ConfigureAwait(false);
        var user = await userStore.FindByEmailAsync(tenant, emailClaim, http.RequestAborted).ConfigureAwait(false);
        if (user is not null)
        {
            await userStore.SetPhoneAsync(tenant, user.Id, null, http.RequestAborted).ConfigureAwait(false);
        }
        return Results.Redirect(AccountPath(options.Value, tenant));
    }

    private static async Task<TenantContext> ResolveAsync(ITenantResolver resolver, HttpContext http)
        => (await resolver.ResolveAsync(http.RequestAborted).ConfigureAwait(false)) ?? TenantContext.Default;

    private static string AccountPath(TapInAuthOptions opts, TenantContext tenant)
        => WithTenant($"{opts.Routes.BasePath.TrimEnd('/')}{opts.Routes.Account}", tenant);

    private static async Task<IResult> SignOutAsync(
        HttpContext http,
        IAuthenticationHandoff handoff,
        ITenantResolver tenantResolver,
        IOptions<TapInAuthOptions> options)
    {
        var tenant = (await tenantResolver.ResolveAsync(http.RequestAborted).ConfigureAwait(false)) ?? TenantContext.Default;
        await handoff.SignOutAsync(new AuthenticationHandoffContext(http, tenant, TapInAuthMethod.None), http.RequestAborted).ConfigureAwait(false);
        // After sign-out, send the user straight to the sign-in page instead of bouncing
        // through the default return URL (typically "/" which would just redirect them back).
        var basePath = options.Value.Routes.BasePath.TrimEnd('/');
        return Results.Redirect(WithTenant($"{basePath}{options.Value.Routes.SignIn}", tenant));
    }

    private static async Task<IResult> CompleteSignInAsync(
        HttpContext http,
        IAuthenticationHandoff handoff,
        IOptions<TapInAuthOptions> options,
        TenantContext tenant,
        AuthenticationResult result)
    {
        if (!result.Succeeded || result.Principal is null)
        {
            // If the user is already signed in and the token is just "already consumed", treat it as a
            // benign re-hit (browser Back, double-click, email-gateway prefetch) and just send them home
            // instead of bouncing them to a confusing sign-in error.
            if (http.User.Identity?.IsAuthenticated == true && result.FailureReason == "consumed")
            {
                return Results.Redirect(options.Value.Routes.DefaultReturnUrl);
            }
            var basePath = options.Value.Routes.BasePath.TrimEnd('/');
            var failurePath = WithTenant($"{basePath}{options.Value.Routes.SignIn}?error={Uri.EscapeDataString(result.FailureReason ?? "failed")}", tenant);
            return Results.Redirect(failurePath);
        }
        await handoff.SignInAsync(new AuthenticationHandoffContext(http, tenant, result.Method, false, result.ReturnUrl), result.Principal, http.RequestAborted).ConfigureAwait(false);
        return Results.Redirect(IsLocalUrl(result.ReturnUrl) ? result.ReturnUrl! : options.Value.Routes.DefaultReturnUrl);
    }

    private static async Task<IFormCollection> ReadFormAsync(HttpContext http)
    {
        if (http.Request.HasFormContentType)
        {
            return await http.Request.ReadFormAsync().ConfigureAwait(false);
        }
        return new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>());
    }

    private static bool IsLocalUrl(string? url)
        => !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal) && !url.StartsWith("/\\", StringComparison.Ordinal);

    /// <summary>
    /// Run the configured <see cref="IRiskSignalProvider"/> (if any) against the captcha token in the
    /// inbound form. Returns null when the request should proceed; returns an IResult when it should be
    /// rejected. No-op when no provider / descriptor is registered.
    /// </summary>
    private static async Task<IResult?> CheckRiskAsync(
        HttpContext http,
        TenantContext tenant,
        string email,
        IFormCollection form,
        IOptions<TapInAuthOptions> options)
    {
        var provider = http.RequestServices.GetService<IRiskSignalProvider>();
        var descriptor = http.RequestServices.GetService<IRiskWidgetDescriptor>();
        if (provider is null || descriptor is null)
        {
            return null;
        }
        var token = form.TryGetValue(descriptor.FormFieldName, out var t) ? t.ToString() : null;
        var ctx = new RiskContext(
            tenant,
            email,
            http.Connection.RemoteIpAddress?.ToString(),
            http.Request.Headers.UserAgent.ToString(),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [descriptor.FormFieldName] = token ?? string.Empty,
            });
        var assessment = await provider.EvaluateAsync(ctx, http.RequestAborted).ConfigureAwait(false);
        if (assessment.Level == RiskLevel.Block)
        {
            var basePath = options.Value.Routes.BasePath.TrimEnd('/');
            var failurePath = WithTenant($"{basePath}{options.Value.Routes.SignIn}?error=bot_check", tenant);
            return Results.Redirect(failurePath);
        }
        return null;
    }

    /// <summary>
    /// Append <c>tenant=&lt;id&gt;</c> to the URL when the tenant is non-default. Used everywhere a
    /// TapInAuth-internal redirect needs to preserve the tenant context across the redirect, since
    /// the resolver re-runs on each request and can't recover the tenant from a tenant-less URL.
    /// </summary>
    private static string WithTenant(string url, TenantContext tenant)
    {
        if (tenant.Id == TenantContext.DefaultTenantId)
        {
            return url;
        }
        var separator = url.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{url}{separator}tenant={Uri.EscapeDataString(tenant.Id)}";
    }
}

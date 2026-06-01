using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using TapInAuth;
using TapInAuth.AspNetCore.DependencyInjection;
using TapInAuth.Email.Smtp.DependencyInjection;
using TapInAuth.Samples.SaaS.Data;
using TapInAuth.Samples.SaaS.Tenancy;
using TapInAuth.Store.EntityFrameworkCore.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=tapinauth-saas.db"));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/auth/sign-in";
        o.LogoutPath = "/auth/sign-out";
        o.ExpireTimeSpan = TimeSpan.FromDays(14);
        o.SlidingExpiration = true;
    });

// Tenant catalog + resolver (dev fallback: ?tenant=acme on a single port).
builder.Services.AddSingleton<InMemoryTenantCatalog>();

builder.Services.AddMail4Dev(o =>
{
    o.SmtpPort = 2525;
    o.EnableImap = true;
    o.ImapPort = 1143;
});

builder.Services.AddTapInAuth(options =>
    {
        options.Methods = TapInAuthMethod.Passkey | TapInAuthMethod.MagicLink | TapInAuthMethod.EmailOtp | TapInAuthMethod.RecoveryCode;
        options.Theme.Accent = "#2563EB";         // global default; per-tenant overrides come from TenantContext.ThemeAccent
        options.FromEmail = "no-reply@tapinauth.local";
        options.FromDisplayName = "TapInAuth SaaS";

        // WebAuthn relying-party for local dev (the catalog can override per tenant via TenantContext.RelyingPartyId).
        options.Relying.Id = "localhost";
        options.Relying.Name = "TapInAuth SaaS";
        options.Relying.AllowedOrigins.Add("https://localhost:5101");
        options.Relying.AllowedOrigins.Add("http://localhost:5100");
    })
    .AddEfCoreStore<AppDbContext>()
    .AddEfCoreAuditSink<AppDbContext>()           // persist audit events — feeds /auth/admin
    .AddTenantResolver<CatalogTenantResolver>()
    .AddSmtpEmail(smtp =>
    {
        smtp.Host = "localhost";
        smtp.Port = 2525;
        smtp.UseStartTls = false;
        smtp.FromAddress = "no-reply@tapinauth.local";
        smtp.FromDisplayName = "TapInAuth SaaS";
    });

builder.Services.AddRazorPages();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseMail4Dev();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
// Block stale cookies from leaking across tenants — fail closed on tapinauth:tenant claim mismatch.
app.UseTenantClaimGuard();
app.MapRazorPages();
app.MapTapInAuth();

app.Run();

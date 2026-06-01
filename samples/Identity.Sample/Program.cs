using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TapInAuth;
using TapInAuth.AspNetCore.DependencyInjection;
using TapInAuth.Email.Smtp.DependencyInjection;
using TapInAuth.Identity.DependencyInjection;
using TapInAuth.Samples.Identity.Data;
using TapInAuth.Store.EntityFrameworkCore.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// ─── Host's DbContext (Identity + TapInAuth share this same context) ──────
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=tapinauth-identity.db"));

// ─── ASP.NET Core Identity setup (host-owned) ────────────────────────────
// This is what an existing Identity app would already have. TapInAuth doesn't replace it.
builder.Services
    .AddIdentityCore<IdentityUser>(o =>
    {
        o.Password.RequireDigit = false;
        o.Password.RequireLowercase = false;
        o.Password.RequireUppercase = false;
        o.Password.RequireNonAlphanumeric = false;
        o.Password.RequiredLength = 1;        // password unused — TapInAuth issues credentials
        o.User.RequireUniqueEmail = true;
        o.SignIn.RequireConfirmedEmail = false; // first sign-in confirms it via magic-link
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/auth/sign-in";
        o.LogoutPath = "/auth/sign-out";
        o.ExpireTimeSpan = TimeSpan.FromDays(14);
        o.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

// ─── Hermex (dev SMTP + inbox at /hermex) ────────────────────────────────
builder.Services.AddMail4Dev(o =>
{
    o.SmtpPort = 2525;
    o.EnableImap = true;
    o.ImapPort = 1143;
});

// ─── TapInAuth — using the Identity adapter instead of EF user store ─────
builder.Services.AddTapInAuth(options =>
    {
        options.Methods = TapInAuthMethod.Passkey | TapInAuthMethod.MagicLink | TapInAuthMethod.EmailOtp;
        options.Theme.Accent = "#059669";    // green to differentiate from the MVC quickstart
        options.FromEmail = "no-reply@tapinauth.local";
        options.FromDisplayName = "TapInAuth Identity Sample";

        options.Relying.Id = "localhost";
        options.Relying.Name = "TapInAuth Identity Sample";
        options.Relying.AllowedOrigins.Add("https://localhost:5201");
        options.Relying.AllowedOrigins.Add("http://localhost:5200");
    })
    .AddEfCoreStore<AppDbContext>()         // magic-link / OTP / credential tables — still EF Core
    .AddIdentityAdapter<IdentityUser>()     // user table — Identity, not TapInAuth's own
    .AddSmtpEmail(smtp =>
    {
        smtp.Host = "localhost";
        smtp.Port = 2525;
        smtp.UseStartTls = false;
        smtp.UseImplicitTls = false;
        smtp.FromAddress = "no-reply@tapinauth.local";
        smtp.FromDisplayName = "TapInAuth Identity Sample";
    });

builder.Services.AddRazorPages();

var app = builder.Build();

// Sample-only: create both Identity and TapInAuth tables on first run.
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

app.MapRazorPages();
app.MapTapInAuth();

app.Run();

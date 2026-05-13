using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
});

builder.Services.AddHttpClient();

var configuredKeysPath = builder.Configuration["DataProtection:KeysPath"];
var keysPath = string.IsNullOrWhiteSpace(configuredKeysPath)
    ? Path.Combine(builder.Environment.ContentRootPath, "DataProtectionKeys")
    : configuredKeysPath;

Directory.CreateDirectory(keysPath);

builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
    .SetApplicationName("PMES");

builder.Services.AddScoped<PmesCSharp.Services.EmailService>();
builder.Services.AddScoped<PmesCSharp.Services.IEmailSender, PmesCSharp.Services.SmtpEmailSender>();
builder.Services.AddScoped<PmesCSharp.Services.IAuditLogger, PmesCSharp.Services.AuditLogger>();
builder.Services.AddScoped<PmesCSharp.Services.IRecaptchaService, PmesCSharp.Services.RecaptchaService>();
builder.Services.AddScoped<PmesCSharp.Services.SubscriptionSettingsService>();
builder.Services.AddScoped<PmesCSharp.Services.EntitlementService>();
builder.Services.AddHttpClient<PmesCSharp.Services.PayMongoService>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<PmesCSharp.Data.ICurrentCompany, PmesCSharp.Data.CurrentCompany>();
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, PmesCSharp.Data.CompanyUserClaimsPrincipalFactory>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, PmesCSharp.Services.BcryptPasswordHasher>();

var google = builder.Configuration.GetSection("Authentication:Google");
var googleClientId = google["ClientId"];
var googleClientSecret = google["ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    builder.Services.AddAuthentication()
        .AddGoogle(options =>
        {
            options.ClientId = googleClientId;
            options.ClientSecret = googleClientSecret;
            // This path is handled by the Google auth middleware.
            // It must be a distinct endpoint from our own controller action.
            options.CallbackPath = "/signin-google/callback";
            options.SignInScheme = Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme;
            options.CorrelationCookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Unspecified;
            options.CorrelationCookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
            options.CorrelationCookie.HttpOnly = true;
            options.CorrelationCookie.IsEssential = true;
            options.Events.OnRemoteFailure = ctx =>
            {
                var error = ctx.Failure?.Message ?? "unknown";
                ctx.Response.Redirect("/login?error=" + Uri.EscapeDataString(error));
                ctx.HandleResponse();
                return Task.CompletedTask;
            };
        });
}

builder.Services.ConfigureExternalCookie(options =>
{
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Unspecified;
    options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
    options.Cookie.IsEssential = true;
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/access/denied";
    options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
});

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}
else
{
    app.UseExceptionHandler("/error");
}

app.UseStatusCodePagesWithReExecute("/error/{0}");

app.UseStaticFiles();
app.UseSession();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");


try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Migration failed.");
}


try
{
    await IdentitySeed.EnsureSeededAsync(app.Services, app.Configuration);
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Seed failed.");
}

app.Run();

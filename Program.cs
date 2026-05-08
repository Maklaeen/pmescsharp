using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

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

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/access/denied";
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

try
{
    await IdentitySeed.EnsureSeededAsync(app.Services, app.Configuration);
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Seed failed.");
}

// Auto-migrate on startup
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

app.Run();

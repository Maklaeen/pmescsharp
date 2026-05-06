using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;
using PmesCSharp.Services;
using PmesCSharp.ViewModels.Settings;

namespace PmesCSharp.Controllers;

[Authorize]
public class SettingsController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditLogger _audit;

    public SettingsController(AppDbContext db, UserManager<ApplicationUser> userManager, IAuditLogger audit)
    {
        _db = db;
        _userManager = userManager;
        _audit = audit;
    }

    [HttpGet("/settings")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Redirect("/login");

        var setting = await _db.UserSettings.FirstOrDefaultAsync(s => s.UserId == user.Id, cancellationToken);
        var vm = new SettingsViewModel
        {
            Theme = setting?.Theme ?? "dark",
        };

        // Uses admin layout for admin-ish users; otherwise default layout.
        if (User.IsInRole("superadmin") || User.IsInRole("admin") || User.IsInRole("planner") || User.IsInRole("inventory") || User.IsInRole("operator") || User.IsInRole("qc"))
        {
            ViewData["Layout"] = "_AdminLayout";
        }

        return View(vm);
    }

    [HttpPost("/settings")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(SettingsViewModel model, CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Redirect("/login");

        if (!ModelState.IsValid)
            return View("Index", model);

        var setting = await _db.UserSettings.FirstOrDefaultAsync(s => s.UserId == user.Id, cancellationToken);
        if (setting is null)
        {
            setting = new UserSetting
            {
                UserId = user.Id,
                Theme = model.Theme,
                UpdatedAt = DateTime.UtcNow,
            };
            _db.UserSettings.Add(setting);
        }
        else
        {
            setting.Theme = model.Theme;
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("user.settings.update", "User", user.Id, $"Theme={model.Theme}", cancellationToken);

        TempData["Success"] = "Settings saved.";
        return Redirect("/settings");
    }

    [HttpPost("/settings/theme")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetTheme([FromForm] string theme, CancellationToken cancellationToken)
    {
        theme = (theme ?? "").Trim().ToLowerInvariant();
        if (theme is not ("light" or "dark"))
            return BadRequest();

        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var setting = await _db.UserSettings.FirstOrDefaultAsync(s => s.UserId == user.Id, cancellationToken);
        if (setting is null)
        {
            _db.UserSettings.Add(new UserSetting { UserId = user.Id, Theme = theme, UpdatedAt = DateTime.UtcNow });
        }
        else
        {
            setting.Theme = theme;
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("user.theme.set", "User", user.Id, $"Theme={theme}", cancellationToken);

        return Redirect(Request.Headers.Referer.ToString());
    }
}

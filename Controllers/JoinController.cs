using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;
using PmesCSharp.ViewModels.Account;

namespace PmesCSharp.Controllers;

[AllowAnonymous]
public class JoinController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public JoinController(AppDbContext db, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        _db = db;
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpGet("/join/{token}")]
    public async Task<IActionResult> Accept(string token)
    {
        var tokenHash = HashToken(token);
        var now = DateTime.UtcNow;

        var invite = await _db.Set<CompanyInvite>()
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash && i.RevokedAt == null && i.ExpiresAt > now && i.UsesCount < i.MaxUses);

        if (invite is null)
        {
            TempData["Error"] = "This invitation is invalid or has expired.";
            return Redirect("/login");
        }

        var vm = new RegisterViewModel { Email = invite.InvitedEmail };
        ViewBag.Token = token;
        ViewBag.Role = invite.Role;
        return View(vm);
    }

    [HttpPost("/join/{token}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptPost(string token, RegisterViewModel model)
    {
        var tokenHash = HashToken(token);
        var now = DateTime.UtcNow;

        var invite = await _db.Set<CompanyInvite>()
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash && i.RevokedAt == null && i.ExpiresAt > now && i.UsesCount < i.MaxUses);

        if (invite is null)
        {
            TempData["Error"] = "This invitation is invalid or has expired.";
            return Redirect("/login");
        }

        ViewBag.Token = token;
        ViewBag.Role = invite.Role;

        ModelState.Remove(nameof(model.CompanyName));
        if (!ModelState.IsValid) return View("Accept", model);

        var existing = await _userManager.FindByEmailAsync(invite.InvitedEmail);
        if (existing is not null)
        {
            ModelState.AddModelError(string.Empty, "An account with this email already exists.");
            return View("Accept", model);
        }

        var user = new ApplicationUser
        {
            UserName = invite.InvitedEmail,
            Email = invite.InvitedEmail,
            FullName = model.Name,
            EmailConfirmed = true,
            CompanyId = invite.CompanyId,
            IsApproved = true,
            ApprovedAt = DateTime.UtcNow,
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError(string.Empty, e.Description);
            return View("Accept", model);
        }

        await _userManager.AddToRoleAsync(user, invite.Role);

        invite.UsesCount++;
        invite.ConsumedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _signInManager.SignInAsync(user, isPersistent: false);
        return Redirect("/dashboard");
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}

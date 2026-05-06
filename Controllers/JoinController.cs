using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;
using PmesCSharp.Services;
using PmesCSharp.ViewModels.Join;

namespace PmesCSharp.Controllers;

[AllowAnonymous]
public class JoinController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAuditLogger _audit;

    public JoinController(AppDbContext db, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IAuditLogger audit)
    {
        _db = db;
        _userManager = userManager;
        _signInManager = signInManager;
        _audit = audit;
    }

    [HttpGet("/join/{token}")]
    public async Task<IActionResult> Invite(string token, CancellationToken cancellationToken)
    {
        var invite = await FindActiveInviteByTokenAsync(token, cancellationToken);
        if (invite is null) return View("InvalidInvite");

        ViewBag.Token = token;

        ViewBag.InvitedEmail = invite.InvitedEmail;
        ViewBag.Role = invite.Role;

        if (User.Identity?.IsAuthenticated == true)
        {
            return Redirect(Url.Action("Accept", new { token })!);
        }

        var registerVm = new JoinRegisterViewModel { Email = invite.InvitedEmail };
        var loginVm = new JoinLoginViewModel { Email = invite.InvitedEmail };
        ViewBag.RegisterModel = registerVm;
        return View("Invite", loginVm);
    }

    [HttpPost("/join/{token}/login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string token, JoinLoginViewModel model, CancellationToken cancellationToken)
    {
        var invite = await FindActiveInviteByTokenAsync(token, cancellationToken);
        if (invite is null) return View("InvalidInvite");

        ViewBag.InvitedEmail = invite.InvitedEmail;
        ViewBag.Role = invite.Role;
        ViewBag.RegisterModel = new JoinRegisterViewModel { Email = invite.InvitedEmail };

        if (!ModelState.IsValid)
            return View("Invite", model);

        if (!string.Equals(model.Email?.Trim(), invite.InvitedEmail, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.Email), "This invite is for a different email address.");
            return View("Invite", model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "No account found for this email. Please register.");
            return View("Invite", model);
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, isPersistent: false, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Invalid credentials.");
            return View("Invite", model);
        }

        // Ensure CompanyId claim is present
        await _signInManager.SignInAsync(user, isPersistent: false);

        return Redirect(Url.Action("Accept", new { token })!);
    }

    [HttpPost("/join/{token}/register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(string token, JoinRegisterViewModel model, CancellationToken cancellationToken)
    {
        var invite = await FindActiveInviteByTokenAsync(token, cancellationToken);
        if (invite is null) return View("InvalidInvite");

        ViewBag.InvitedEmail = invite.InvitedEmail;
        ViewBag.Role = invite.Role;

        // For redisplay of login form
        var loginVm = new JoinLoginViewModel { Email = invite.InvitedEmail };

        if (!ModelState.IsValid)
        {
            ViewBag.RegisterModel = model;
            return View("Invite", loginVm);
        }

        if (!string.Equals(model.Email?.Trim(), invite.InvitedEmail, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.Email), "This invite is for a different email address.");
            ViewBag.RegisterModel = model;
            return View("Invite", loginVm);
        }

        var existing = await _userManager.FindByEmailAsync(model.Email);
        if (existing is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "Email already registered. Please log in.");
            ViewBag.RegisterModel = model;
            return View("Invite", loginVm);
        }

        // Create user as pending approval in invited company.
        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.Name,
            EmailConfirmed = true,
            CompanyId = invite.CompanyId,
          IsApproved = false,
            ApprovedAt = null,
            PendingRole = invite.Role,
        };

        var createResult = await _userManager.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            foreach (var e in createResult.Errors)
                ModelState.AddModelError(string.Empty, e.Description);
            ViewBag.RegisterModel = model;
            return View("Invite", loginVm);
        }

        // Role is stored in invite; admin will approve later.
        TempData["Status"] = "Account created. Pending approval before login.";

        await _audit.LogAsync("invite.join.register", "User", user.Id, $"Registered via invite; pending approval; role={invite.Role}", cancellationToken);

        return Redirect(Url.Action("Pending")!);
    }

    [Authorize]
    [HttpGet("/join/{token}/accept")]
    public async Task<IActionResult> Accept(string token, CancellationToken cancellationToken)
    {
        var invite = await FindActiveInviteByTokenAsync(token, cancellationToken);
        if (invite is null) return View("InvalidInvite");

        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Redirect("/login");

        if (!string.Equals(user.Email, invite.InvitedEmail, StringComparison.OrdinalIgnoreCase))
            return Forbid();

        // Attach to company and mark as pending approval (archived users cannot log in to dashboard)
        user.CompanyId = invite.CompanyId;
     user.IsApproved = false;
        user.ApprovedAt = null;
        user.PendingRole = invite.Role;
        await _userManager.UpdateAsync(user);

        invite.UsesCount += 1;
        invite.ConsumedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync("invite.accept", "CompanyInvite", invite.Id.ToString(), $"Accepted by {user.Email}; pending approval", cancellationToken);

        return Redirect(Url.Action("Pending")!);
    }

    [HttpGet("/join/pending")]
    public IActionResult Pending()
    {
        return View();
    }

    private async Task<CompanyInvite?> FindActiveInviteByTokenAsync(string token, CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(token);
        var now = DateTime.UtcNow;
        return await _db.Set<CompanyInvite>()
            .Include(i => i.Company)
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash && i.RevokedAt == null && i.ExpiresAt > now && i.UsesCount < i.MaxUses, cancellationToken);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}

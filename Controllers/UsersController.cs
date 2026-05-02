using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Models;
using PmesCSharp.ViewModels.Users;

namespace PmesCSharp.Controllers;

[Authorize(Roles = "superadmin,admin")]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;

    private static readonly string[] AllRoles = ["superadmin", "admin", "planner", "inventory", "operator", "qc"];

    public UsersController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet("/admin/users")]
    public async Task<IActionResult> Index([FromQuery] int page = 1, [FromQuery] bool archived = false)
    {
        const int pageSize = 10;
        var query = _userManager.Users.Where(u => u.IsArchived == archived);
        var users = await query
            .OrderByDescending(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var userRoles = new Dictionary<string, string>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userRoles[user.Id] = roles.FirstOrDefault() ?? "superadmin";
        }

        var totalUsers = await query.CountAsync();
        ViewBag.UserRoles = userRoles;
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalUsers / (double)pageSize);
        ViewBag.Archived = archived;
        return View(users);
    }

    [HttpGet("/admin/users/create")]
    public IActionResult Create()
    {
        ViewBag.Roles = AllRoles;
        return View(new UserFormViewModel());
    }

    [HttpPost("/admin/users/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Store(UserFormViewModel model)
    {
        ViewBag.Roles = AllRoles;

        if (string.IsNullOrWhiteSpace(model.Password))
            ModelState.AddModelError(nameof(model.Password), "Password is required.");

        if (!ModelState.IsValid)
            return View("Create", model);

        var existing = await _userManager.FindByEmailAsync(model.Email);
        if (existing is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "Email already taken.");
            return View("Create", model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.Name,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, model.Password!);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError(string.Empty, e.Description);
            return View("Create", model);
        }

        if (!string.IsNullOrWhiteSpace(model.Role))
            await _userManager.AddToRoleAsync(user, model.Role);

        TempData["Success"] = "User created successfully.";
        return Redirect("/admin/users");
    }

    [HttpGet("/admin/users/{id}/edit")]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        var roles = await _userManager.GetRolesAsync(user);

        // Prevent admin from editing superadmin
        if (roles.Contains("superadmin") && !User.IsInRole("superadmin"))
        {
            TempData["Error"] = "You do not have permission to edit a superadmin account.";
            return Redirect("/admin/users");
        }
        var vm = new UserFormViewModel
        {
            Name = user.FullName ?? "",
            Email = user.Email ?? "",
            Role = roles.FirstOrDefault()
        };

        ViewBag.Roles = AllRoles;
        ViewBag.UserId = id;
        return View(vm);
    }

    [HttpPost("/admin/users/{id}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(string id, UserFormViewModel model)
    {
        ViewBag.Roles = AllRoles;
        ViewBag.UserId = id;

        if (!ModelState.IsValid)
            return View("Edit", model);

        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        // Prevent admin from editing superadmin
        var targetRoles = await _userManager.GetRolesAsync(user);
        if (targetRoles.Contains("superadmin") && !User.IsInRole("superadmin"))
        {
            TempData["Error"] = "You do not have permission to edit a superadmin account.";
            return Redirect("/admin/users");
        }

        user.FullName = model.Name;
        user.Email = model.Email;
        user.UserName = model.Email;
        user.NormalizedEmail = model.Email.ToUpperInvariant();
        user.NormalizedUserName = model.Email.ToUpperInvariant();

        await _userManager.UpdateAsync(user);

        // Update role
        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        if (!string.IsNullOrWhiteSpace(model.Role))
            await _userManager.AddToRoleAsync(user, model.Role);

        // Update password if provided
        if (!string.IsNullOrWhiteSpace(model.Password))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            await _userManager.ResetPasswordAsync(user, token, model.Password);
        }

        TempData["Success"] = "User updated successfully.";
        return Redirect("/admin/users");
    }

    [HttpPost("/admin/users/{id}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Destroy(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is not null)
        {
            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("superadmin") && !User.IsInRole("superadmin"))
            {
                TempData["Error"] = "You do not have permission to delete a superadmin account.";
                return Redirect("/admin/users");
            }

            user.IsArchived = true;
            user.ArchivedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
        }

        TempData["Success"] = "User archived.";
        return Redirect("/admin/users");
    }

    [HttpPost("/admin/users/{id}/restore")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is not null)
        {
            user.IsArchived = false;
            user.ArchivedAt = null;
            await _userManager.UpdateAsync(user);
        }

        TempData["Success"] = "User restored.";
        return Redirect("/admin/users?archived=true");
    }
}

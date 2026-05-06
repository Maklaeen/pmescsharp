using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;
using PmesCSharp.Services;
using PmesCSharp.ViewModels.Users;

namespace PmesCSharp.Controllers;

[Authorize(Roles = "superadmin,admin")]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentCompany _currentCompany;
    private readonly IAuditLogger _audit;

    private static readonly string[] AllRoles = ["superadmin", "admin", "planner", "inventory", "operator", "qc"];

    public UsersController(UserManager<ApplicationUser> userManager, ICurrentCompany currentCompany, IAuditLogger audit)
    {
        _userManager = userManager;
       _currentCompany = currentCompany;
       _audit = audit;
    }

    [HttpGet("/admin/users")]
    public async Task<IActionResult> Index([FromQuery] int page = 1, [FromQuery] bool archived = false)
    {
        const int pageSize = 10;
        var companyId = _currentCompany.CompanyId;
        var query = _userManager.Users.Where(u => u.IsArchived == archived && u.CompanyId == companyId);
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

    [HttpGet("/admin/users/pending")]
    public async Task<IActionResult> Pending(CancellationToken cancellationToken)
    {
        var companyId = _currentCompany.CompanyId;
        if (!User.IsInRole("superadmin") && companyId <= 0) return Forbid();

        var query = _userManager.Users
            .Where(u => !u.IsApproved && u.CompanyId == companyId)
          .OrderByDescending(u => u.Id);

        var items = await query
            .Select(u => new PmesCSharp.ViewModels.Users.PendingApprovalUserViewModel
            {
                Id = u.Id,
                Name = u.FullName,
                Email = u.Email ?? "",
                PendingRole = u.PendingRole ?? "",
             RequestedAt = null,
            })
            .ToListAsync(cancellationToken);

        return View(items);
    }

    [HttpPost("/admin/users/{id}/approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(string id, CancellationToken cancellationToken)
    {
        var companyId = _currentCompany.CompanyId;
        if (companyId <= 0) return Forbid();

        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (user.CompanyId != companyId) return NotFound();
        if (user.IsApproved)
        {
            TempData["Success"] = "User already approved.";
            return Redirect("/admin/users/pending");
        }

        user.IsApproved = true;
        user.ApprovedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var role = string.IsNullOrWhiteSpace(user.PendingRole) ? "operator" : user.PendingRole;
        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRoleAsync(user, role);
        user.PendingRole = null;
        await _userManager.UpdateAsync(user);

        await _audit.LogAsync("user.approve", "User", user.Id, $"Approved {user.Email}; role={role}", cancellationToken);

        TempData["Success"] = "User approved.";
        return Redirect("/admin/users/pending");
    }

    [HttpPost("/admin/users/{id}/reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(string id, CancellationToken cancellationToken)
    {
        var companyId = _currentCompany.CompanyId;
        if (companyId <= 0) return Forbid();

        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (user.CompanyId != companyId) return NotFound();

        // Reject = archive account and clear company assignment
        user.IsApproved = false;
        user.PendingRole = null;
        user.IsArchived = true;
        user.ArchivedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        await _audit.LogAsync("user.reject", "User", user.Id, $"Rejected {user.Email}", cancellationToken);
        TempData["Success"] = "User rejected.";
        return Redirect("/admin/users/pending");
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
           EmailConfirmed = true,
            CompanyId = _currentCompany.CompanyId,
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

        // Prevent cross-company edits
        if (user.CompanyId != _currentCompany.CompanyId)
            return NotFound();

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

        // Prevent cross-company edits
        if (user.CompanyId != _currentCompany.CompanyId)
            return NotFound();

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
       if (user is not null && user.CompanyId != _currentCompany.CompanyId)
            return NotFound();
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
       if (user is not null && user.CompanyId != _currentCompany.CompanyId)
            return NotFound();
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

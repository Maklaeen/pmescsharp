using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;
using PmesCSharp.Services;
using PmesCSharp.ViewModels.Company;
using System.Collections.Generic;

namespace PmesCSharp.Controllers;

[Authorize]
public class CompanyController : Controller
{
    private readonly AppDbContext _db;
    private readonly ICurrentCompany _currentCompany;
    private readonly IAuditLogger _audit;
    private readonly UserManager<ApplicationUser> _userManager;

    public CompanyController(AppDbContext db, ICurrentCompany currentCompany, IAuditLogger audit, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _currentCompany = currentCompany;
        _audit = audit;
        _userManager = userManager;
    }

    [HttpGet("/company/profile")]
    [HttpGet("/company")]
    [HttpGet("/company/settings")]
    public async Task<IActionResult> Profile()
    {
        var companyId = _currentCompany.CompanyId;

        if (User.IsInRole("superadmin"))
        {
            var showArchived = Request.Query["archived"] == "true";
            var allCompanies = await _db.Companies
                .AsNoTracking()
                .Where(c => c.IsArchived == showArchived)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
            ViewBag.ShowArchived = showArchived;
            ViewBag.ArchivedCount = await _db.Companies.CountAsync(c => c.IsArchived);
            ViewBag.ActiveCount = await _db.Companies.CountAsync(c => !c.IsArchived);
            return View("Companies", allCompanies);
        }

        if (companyId <= 0) return Forbid();

        var company = await _db.Companies.FirstOrDefaultAsync(c => c.Id == companyId);
        if (company is null) return NotFound();

        var profile = await _db.CompanyProfiles.FirstOrDefaultAsync(p => p.CompanyId == companyId);
        var users = await _userManager.Users
            .Where(u => u.CompanyId == companyId)
            .ToListAsync();

        var userItems = new List<CompanyProfileUserViewModel>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userItems.Add(new CompanyProfileUserViewModel
            {
                Name = string.IsNullOrWhiteSpace(user.FullName) ? user.Email ?? "Unknown" : user.FullName,
                Email = user.Email ?? "",
                Role = roles.FirstOrDefault() ?? "unassigned"
            });
        }

        var vm = new CompanyProfileViewModel
        {
            DisplayName = profile?.DisplayName ?? company.Name,
            Address = profile?.Address,
            Phone = profile?.Phone,
            Email = profile?.Email,
            Website = profile?.Website,
            Industry = profile?.Industry,
            Users = userItems,
        };

        return View(vm);
    }

    [HttpPost("/company/profile")]
    [HttpPost("/company")]
    [HttpPost("/company/settings")]
    [Authorize(Roles = "superadmin,admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProfile(CompanyProfileViewModel model, CancellationToken cancellationToken)
    {
        var companyId = _currentCompany.CompanyId;
        if (!User.IsInRole("superadmin") && companyId <= 0)
            return Forbid();

        if (!ModelState.IsValid)
        {
            var users = await _userManager.Users
                .Where(u => u.CompanyId == companyId)
                .ToListAsync();

            var userItems = new List<CompanyProfileUserViewModel>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userItems.Add(new CompanyProfileUserViewModel
                {
                    Name = string.IsNullOrWhiteSpace(user.FullName) ? user.Email ?? "Unknown" : user.FullName,
                    Email = user.Email ?? "",
                    Role = roles.FirstOrDefault() ?? "unassigned"
                });
            }

            model.Users = userItems;
            var companyShort = await _db.Companies.FirstOrDefaultAsync(c => c.Id == companyId);
            var profileShort = await _db.CompanyProfiles.FirstOrDefaultAsync(p => p.CompanyId == companyId);
            ViewBag.ProfileUpdatedAt = (profileShort?.UpdatedAt ?? companyShort?.UpdatedAt)?.ToString("MMMM dd, yyyy");
            return View("Profile", model);
        }

        var company = await _db.Companies.FirstOrDefaultAsync(c => c.Id == companyId, cancellationToken);
        if (company is null) return NotFound();

        var profile = await _db.CompanyProfiles.FirstOrDefaultAsync(p => p.CompanyId == companyId, cancellationToken);
        if (profile is not null)
        {
            var nextAllowed = profile.UpdatedAt.AddMonths(1);
            if (DateTime.UtcNow < nextAllowed)
            {
                TempData["Error"] = $"Company profile can only be updated once every 30 days. You can update it again on {nextAllowed:MMMM dd, yyyy}.";

                ViewBag.NextAllowed = nextAllowed.ToString("MMMM dd, yyyy");
                ViewBag.ProfileUpdatedAt = profile.UpdatedAt.ToString("MMMM dd, yyyy");

                var users = await _userManager.Users
                    .Where(u => u.CompanyId == companyId)
                    .ToListAsync();

                var userItems = new List<CompanyProfileUserViewModel>();
                foreach (var user in users)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    userItems.Add(new CompanyProfileUserViewModel
                    {
                        Name = string.IsNullOrWhiteSpace(user.FullName) ? user.Email ?? "Unknown" : user.FullName,
                        Email = user.Email ?? "",
                        Role = roles.FirstOrDefault() ?? "unassigned"
                    });
                }

                model.Users = userItems;
                return View("Profile", model);
            }
        }

        if (profile is null)
        {
            profile = new CompanyProfile
            {
                CompanyId = companyId,
                DisplayName = model.DisplayName,
                Address = model.Address,
                Phone = model.Phone,
                Email = model.Email,
                Website = model.Website,
                Industry = model.Industry,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _db.CompanyProfiles.Add(profile);
        }
        else
        {
            profile.DisplayName = model.DisplayName;
            profile.Address = model.Address;
            profile.Phone = model.Phone;
            profile.Email = model.Email;
            profile.Website = model.Website;
            profile.Industry = model.Industry;
            profile.UpdatedAt = DateTime.UtcNow;
        }

        company.Name = string.IsNullOrWhiteSpace(model.DisplayName) ? company.Name : model.DisplayName;
        company.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync("company.profile.update", "Company", companyId.ToString(), $"Updated company profile: {model.DisplayName}", cancellationToken);

        TempData["Success"] = "Company profile updated.";
        return Redirect("/company");
    }

    private async Task DeleteCompanyAndRelatedDataAsync(int companyId, CancellationToken ct = default)
    {
        // Delete in correct order to avoid FK violations
        var companyUsers = await _userManager.Users
            .Where(u => u.CompanyId == companyId)
            .ToListAsync(ct);
        foreach (var u in companyUsers)
            await _userManager.DeleteAsync(u);

        // Delete related company data
        var subscriptions = _db.CompanySubscriptions.Where(s => s.CompanyId == companyId);
        _db.CompanySubscriptions.RemoveRange(subscriptions);

        var profiles = _db.CompanyProfiles.Where(p => p.CompanyId == companyId);
        _db.CompanyProfiles.RemoveRange(profiles);

        var invites = _db.Set<PmesCSharp.Models.CompanyInvite>().Where(i => i.CompanyId == companyId);
        _db.Set<PmesCSharp.Models.CompanyInvite>().RemoveRange(invites);

        await _db.SaveChangesAsync(ct);

        var company = await _db.Companies.FindAsync(companyId);
        if (company is not null)
        {
            _db.Companies.Remove(company);
            await _db.SaveChangesAsync(ct);
        }
    }

    [HttpPost("/superadmin/companies/{id:int}/status-archive")]
    [Authorize(Roles = "superadmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id, CancellationToken ct)
    {
        var company = await _db.Companies.FindAsync(id);
        if (company is null) return NotFound();

        company.IsArchived = true;
        company.ArchivedAt = DateTime.UtcNow;
        company.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("company.archive", "Company", company.Id.ToString(), $"Archived company {company.Name}");
        TempData["Success"] = $"{company.Name} has been archived.";
        return Redirect("/company");
    }

    [HttpPost("/superadmin/companies/{id:int}/status-unarchive")]
    [Authorize(Roles = "superadmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unarchive(int id, CancellationToken ct)
    {
        var company = await _db.Companies.FindAsync(id);
        if (company is null) return NotFound();

        company.IsArchived = false;
        company.ArchivedAt = null;
        company.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("company.unarchive", "Company", company.Id.ToString(), $"Restored company {company.Name}");
        TempData["Success"] = $"{company.Name} has been unarchived.";
        return Redirect("/company?archived=true");
    }

    [HttpPost("/superadmin/companies/{id:int}/status-delete")]
    [Authorize(Roles = "superadmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCompany(int id, CancellationToken ct)
    {
        var company = await _db.Companies.FindAsync(id);
        if (company is null) return NotFound();
        if (!company.IsArchived)
        {
            TempData["Error"] = "Only archived companies can be deleted.";
            return Redirect("/company");
        }

        var name = company.Name;
        try
        {
            await DeleteCompanyAndRelatedDataAsync(id, ct);
            await _audit.LogAsync("company.delete", "Company", id.ToString(), $"Permanently deleted company {name}");
            TempData["Success"] = $"{name} has been permanently deleted.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Failed to delete company: {ex.Message}";
        }

        return Redirect("/company?archived=true");
    }
}

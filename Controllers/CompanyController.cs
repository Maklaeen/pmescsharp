using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;
using PmesCSharp.Services;
using PmesCSharp.ViewModels.Company;

namespace PmesCSharp.Controllers;

[Authorize]
public class CompanyController : Controller
{
    private readonly AppDbContext _db;
    private readonly ICurrentCompany _currentCompany;
    private readonly IAuditLogger _audit;

    public CompanyController(AppDbContext db, ICurrentCompany currentCompany, IAuditLogger audit)
    {
        _db = db;
        _currentCompany = currentCompany;
        _audit = audit;
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
        var vm = new CompanyProfileViewModel
        {
            DisplayName = profile?.DisplayName ?? company.Name,
            Address = profile?.Address,
            Phone = profile?.Phone,
            Email = profile?.Email,
            Website = profile?.Website,
            Industry = profile?.Industry,
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
            return View("Profile", model);

        var company = await _db.Companies.FirstOrDefaultAsync(c => c.Id == companyId, cancellationToken);
        if (company is null) return NotFound();

        var profile = await _db.CompanyProfiles.FirstOrDefaultAsync(p => p.CompanyId == companyId, cancellationToken);
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

        company.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync("company.profile.update", "Company", companyId.ToString(), $"Updated company profile: {model.DisplayName}", cancellationToken);

        TempData["Success"] = "Company profile updated.";
        return Redirect("/company");
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

        _db.Companies.Remove(company);
        await _db.SaveChangesAsync(ct);

        TempData["Success"] = $"{company.Name} has been permanently deleted.";
        return Redirect("/company?archived=true");
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;
using PmesCSharp.ViewModels.Company;

namespace PmesCSharp.Controllers;

[Authorize(Roles = "admin")]
public class OnboardingController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentCompany _currentCompany;

    public OnboardingController(AppDbContext db, UserManager<ApplicationUser> userManager, ICurrentCompany currentCompany)
    {
        _db = db;
        _userManager = userManager;
        _currentCompany = currentCompany;
    }

    // Step 1 — Company Profile
    [HttpGet("/onboarding/company")]
    public async Task<IActionResult> Company()
    {
        var companyId = _currentCompany.CompanyId;
        var company = await _db.Companies.FirstOrDefaultAsync(c => c.Id == companyId);
        if (company is null) return Redirect("/admin");

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

        ViewBag.CompanyName = company.Name;
        return View(vm);
    }

    [HttpPost("/onboarding/company")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCompany(CompanyProfileViewModel model, CancellationToken ct)
    {
        var companyId = _currentCompany.CompanyId;
        var company = await _db.Companies.FirstOrDefaultAsync(c => c.Id == companyId, ct);
        if (company is null) return Redirect("/admin");

        var profile = await _db.CompanyProfiles.FirstOrDefaultAsync(p => p.CompanyId == companyId, ct);
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
        await _db.SaveChangesAsync(ct);

        return Redirect("/onboarding/profile");
    }

    // Step 2 — User Profile
    [HttpGet("/onboarding/profile")]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Redirect("/login");
        return View(user);
    }

    [HttpPost("/onboarding/profile")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProfile(
        [FromForm] string name,
        [FromForm] string? mobileNumber,
        [FromForm] string? sex,
        CancellationToken ct)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Redirect("/login");

        user.FullName = name;
        user.MobileNumber = string.IsNullOrWhiteSpace(mobileNumber) ? null : mobileNumber.Trim();
        user.Sex = string.IsNullOrWhiteSpace(sex) ? null : sex.Trim();
        await _userManager.UpdateAsync(user);

        TempData["Success"] = "Welcome to PMES! Your account is ready.";
        return Redirect("/admin");
    }
}

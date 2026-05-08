using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;
using PmesCSharp.Services;
using PmesCSharp.ViewModels.Subscription;

namespace PmesCSharp.Controllers;

[Authorize(Roles = "superadmin,admin")]
public class SubscriptionController : Controller
{
    private readonly AppDbContext _db;
    private readonly ICurrentCompany _currentCompany;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditLogger _audit;

    public SubscriptionController(AppDbContext db, ICurrentCompany currentCompany, UserManager<ApplicationUser> userManager, IAuditLogger audit)
    {
        _db = db;
        _currentCompany = currentCompany;
        _userManager = userManager;
        _audit = audit;
    }

    [HttpGet("/subscription")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var companyId = _currentCompany.CompanyId;
        if (companyId <= 0) return Forbid();

      var sub = await _db.Set<CompanySubscription>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CompanyId == companyId, cancellationToken);

        if (sub is null)
            return Redirect("/subscription/setup");

        return View(sub);
    }

    [HttpGet("/subscription/setup")]
    public async Task<IActionResult> Setup(CancellationToken cancellationToken)
    {
        var companyId = _currentCompany.CompanyId;
        if (companyId <= 0) return Forbid();

        var existing = await _db.Set<CompanySubscription>().AnyAsync(s => s.CompanyId == companyId, cancellationToken);
        if (existing)
            return Redirect("/subscription");

        var user = await _userManager.GetUserAsync(User);
        var vm = new SubscriptionSetupViewModel
        {
            Plan = SubscriptionPlan.Standard,
            BillingEmail = user?.Email,
        };

        return View(vm);
    }

    [HttpPost("/subscription/setup")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSetup(SubscriptionSetupViewModel model, CancellationToken cancellationToken)
    {
        var companyId = _currentCompany.CompanyId;
        if (companyId <= 0) return Forbid();

        if (!ModelState.IsValid)
            return View("Setup", model);

        var existing = await _db.Set<CompanySubscription>().AnyAsync(s => s.CompanyId == companyId, cancellationToken);
        if (existing)
            return Redirect("/subscription");

        var now = DateTime.UtcNow;
        var sub = new CompanySubscription
        {
            CompanyId = companyId,
            Plan = model.Plan,
            Status = SubscriptionStatus.Trialing,
            TrialEndsAt = now.AddDays(30),
            CurrentPeriodEndsAt = now.AddDays(30),
            BillingEmail = string.IsNullOrWhiteSpace(model.BillingEmail) ? null : model.BillingEmail.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.Add(sub);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync("subscription.setup", "CompanySubscription", sub.Id.ToString(), $"Plan={sub.Plan}; status={sub.Status}; trialEnds={sub.TrialEndsAt:O}", cancellationToken);

        TempData["Success"] = "Subscription initialized. You are on a 30-day free trial.";
        return Redirect("/admin/dashboard");
    }
}

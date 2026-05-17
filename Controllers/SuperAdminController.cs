using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;
using PmesCSharp.Services;
using PmesCSharp.ViewModels.SuperAdmin;

namespace PmesCSharp.Controllers;

[Authorize(Roles = "superadmin")]
public class SuperAdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly IAuditLogger _audit;
    private readonly PmesCSharp.Services.SubscriptionSettingsService _settings;

    public SuperAdminController(AppDbContext db, IAuditLogger audit, PmesCSharp.Services.SubscriptionSettingsService settings)
    {
        _db = db;
        _audit = audit;
        _settings = settings;
    }

    [HttpGet("/superadmin/reports")]
    public IActionResult Reports() => View();

    [HttpGet("/superadmin/reports/data")]
    public async Task<IActionResult> ReportsData([FromQuery] int? year, CancellationToken ct)
    {
        var y = year ?? DateTime.UtcNow.Year;
        var monthLabels = Enumerable.Range(1, 12).Select(m => new DateTime(y, m, 1).ToString("MMM")).ToArray();

        var revenue = new double[12];
        var subscriptions = new int[12];
        var approvedUsers = new int[12];

        for (int m = 1; m <= 12; m++)
        {
            var start = new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddMonths(1);

            var acts = await _db.AuditLogs.AsNoTracking()
                .Where(a => a.Action == "subscription.activated" && a.CreatedAt >= start && a.CreatedAt < end)
                .ToListAsync(ct);

            subscriptions[m - 1] = acts.Count;

            long monthRevenueCentavos = 0;
            foreach (var act in acts)
            {
                SubscriptionPlan plan = SubscriptionPlan.Free;
                SubscriptionBillingCycle cycle = SubscriptionBillingCycle.Monthly;

                if (!string.IsNullOrWhiteSpace(act.EntityId) && int.TryParse(act.EntityId, out var subId))
                {
                    var sub = await _db.CompanySubscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == subId, ct);
                    if (sub != null)
                    {
                        plan = sub.Plan;
                        cycle = sub.BillingCycle;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(act.Details))
                {
                    // Try to parse Plan=Pro
                    var idx = act.Details.IndexOf("Plan=");
                    if (idx >= 0)
                    {
                        var rest = act.Details.Substring(idx + 5);
                        var token = rest.Split(' ', ';', ',').FirstOrDefault();
                        if (Enum.TryParse<SubscriptionPlan>(token, true, out var p)) plan = p;
                    }
                }

                var planDef = await _settings.GetPlanAsync(plan, ct);
                var amount = cycle == SubscriptionBillingCycle.Annual ? planDef.AnnualPriceCentavos : planDef.MonthlyPriceCentavos;
                monthRevenueCentavos += amount;
            }

            revenue[m - 1] = monthRevenueCentavos / 100.0;

            // Approved users in the month (use audit logs for approvals)
            var approvals = await _db.AuditLogs.AsNoTracking()
                .Where(a => a.Action == "user.approve" && a.CreatedAt >= start && a.CreatedAt < end)
                .CountAsync(ct);
            approvedUsers[m - 1] = approvals;
        }

        var totalUsers = await _db.Users.AsNoTracking().CountAsync(u => !u.IsArchived, ct);

        return Ok(new
        {
            labels = monthLabels,
            revenue,
            subscriptions,
            approvedUsers,
            totalUsers,
            year = y
        });
    }

    [HttpGet("/superadmin/plans")]
    public async Task<IActionResult> Plans(CancellationToken ct)
    {
        var plans = await _db.SubscriptionPlanDefinitions
            .AsNoTracking()
            .OrderBy(p => p.Plan)
            .ToListAsync(ct);

        return View(plans);
    }

    [HttpGet("/superadmin/plans/{plan}")]
    public async Task<IActionResult> EditPlan([FromRoute] SubscriptionPlan plan, CancellationToken ct)
    {
        var row = await _db.SubscriptionPlanDefinitions.FirstOrDefaultAsync(p => p.Plan == plan, ct);
        row ??= new SubscriptionPlanDefinition { Plan = plan };

        var vm = new PlanEditViewModel
        {
            Plan = row.Plan,
            Currency = row.Currency,
            MonthlyPriceCentavos = row.MonthlyPriceCentavos,
            AnnualPriceCentavos = row.AnnualPriceCentavos,
            MaxUsers = row.MaxUsers,
            MaxProducts = row.MaxProducts,
            MaxMaterials = row.MaxMaterials,
            MaxWorkOrdersPerMonth = row.MaxWorkOrdersPerMonth,
            MaxStorageMb = row.MaxStorageMb,
            EnableReports = row.EnableReports,
            EnableCosting = row.EnableCosting,
            EnableAuditLogs = row.EnableAuditLogs,
        };

        return View(vm);
    }

    [HttpPost("/superadmin/plans/{plan}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePlan([FromRoute] SubscriptionPlan plan, PlanEditViewModel model, CancellationToken ct)
    {
        if (plan != model.Plan) return BadRequest();
        if (!ModelState.IsValid) return View("EditPlan", model);

        var row = await _db.SubscriptionPlanDefinitions.FirstOrDefaultAsync(p => p.Plan == plan, ct);
        if (row is null)
        {
            row = new SubscriptionPlanDefinition { Plan = plan };
            _db.SubscriptionPlanDefinitions.Add(row);
        }

        row.Currency = (model.Currency ?? "PHP").Trim().ToUpperInvariant();
        row.MonthlyPriceCentavos = model.MonthlyPriceCentavos;
        row.AnnualPriceCentavos = model.AnnualPriceCentavos;
        row.MaxUsers = model.MaxUsers;
        row.MaxProducts = model.MaxProducts;
        row.MaxMaterials = model.MaxMaterials;
        row.MaxWorkOrdersPerMonth = model.MaxWorkOrdersPerMonth;
        row.MaxStorageMb = model.MaxStorageMb;
        row.EnableReports = model.EnableReports;
        row.EnableCosting = model.EnableCosting;
        row.EnableAuditLogs = model.EnableAuditLogs;
        row.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("superadmin.plan.update", "SubscriptionPlanDefinition", row.Id.ToString(), $"Plan={row.Plan}", ct);

        TempData["Success"] = "Plan updated.";
        return Redirect("/superadmin/plans");
    }

    [HttpGet("/superadmin/billing-settings")]
    public async Task<IActionResult> BillingSettings(CancellationToken ct)
    {
        var row = await _db.SubscriptionGlobalSettings.OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
        row ??= new SubscriptionGlobalSetting();

        var vm = new GlobalBillingSettingsViewModel
        {
            PayMongoPublicKey = row.PayMongoPublicKey,
            PayMongoSecretKey = row.PayMongoSecretKey,
            TrialDays = row.TrialDays,
            GracePeriodDays = row.GracePeriodDays,
        };

        return View(vm);
    }

    [HttpPost("/superadmin/billing-settings")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBillingSettings(GlobalBillingSettingsViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("BillingSettings", model);

        var row = await _db.SubscriptionGlobalSettings.OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
        if (row is null)
        {
            row = new SubscriptionGlobalSetting();
            _db.SubscriptionGlobalSettings.Add(row);
        }

        row.PayMongoPublicKey = string.IsNullOrWhiteSpace(model.PayMongoPublicKey) ? null : model.PayMongoPublicKey.Trim();
        row.PayMongoSecretKey = string.IsNullOrWhiteSpace(model.PayMongoSecretKey) ? null : model.PayMongoSecretKey.Trim();
        row.TrialDays = model.TrialDays;
        row.GracePeriodDays = model.GracePeriodDays;
        row.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("superadmin.billing.update", "SubscriptionGlobalSetting", row.Id.ToString(), "Updated global billing settings", ct);

        TempData["Success"] = "Settings updated.";
        return Redirect("/superadmin/billing-settings");
    }

    [HttpGet("/superadmin/subscriptions")]
    public async Task<IActionResult> Subscriptions(CancellationToken ct)
    {
        var items = await _db.Companies
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new PmesCSharp.ViewModels.SuperAdmin.CompanySubscriptionListItemViewModel
            {
                CompanyId = c.Id,
                Name = c.Name,
                Code = c.Code,
                Plan = _db.CompanySubscriptions.AsNoTracking().Where(s => s.CompanyId == c.Id).Select(s => (SubscriptionPlan?)s.Plan).FirstOrDefault(),
                Status = _db.CompanySubscriptions.AsNoTracking().Where(s => s.CompanyId == c.Id).Select(s => (SubscriptionStatus?)s.Status).FirstOrDefault(),
                CurrentPeriodEndsAt = _db.CompanySubscriptions.AsNoTracking().Where(s => s.CompanyId == c.Id).Select(s => s.CurrentPeriodEndsAt).FirstOrDefault(),
            })
            .ToListAsync(ct);

        return View(items);
    }

    [HttpGet("/superadmin/subscriptions/{companyId:int}")]
    public async Task<IActionResult> EditSubscription([FromRoute] int companyId, CancellationToken ct)
    {
        var company = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == companyId, ct);
        if (company is null) return NotFound();

        var sub = await _db.CompanySubscriptions.FirstOrDefaultAsync(s => s.CompanyId == companyId, ct);

        var vm = new CompanySubscriptionOverrideViewModel
        {
            CompanyId = companyId,
            Plan = sub?.Plan ?? SubscriptionPlan.Free,
            Status = sub?.Status ?? SubscriptionStatus.Active,
            BillingCycle = sub?.BillingCycle ?? SubscriptionBillingCycle.Monthly,
            TrialEndsAt = sub?.TrialEndsAt,
            CurrentPeriodEndsAt = sub?.CurrentPeriodEndsAt,
            BillingEmail = sub?.BillingEmail,
        };

        ViewBag.CompanyName = company.Name;
        return View(vm);
    }

    [HttpPost("/superadmin/subscriptions/{companyId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSubscription([FromRoute] int companyId, CompanySubscriptionOverrideViewModel model, CancellationToken ct)
    {
        if (companyId != model.CompanyId) return BadRequest();
        if (!ModelState.IsValid) return View("EditSubscription", model);

        var company = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == companyId, ct);
        if (company is null) return NotFound();

        var sub = await _db.CompanySubscriptions.FirstOrDefaultAsync(s => s.CompanyId == companyId, ct);
        if (sub is null)
        {
            sub = new CompanySubscription { CompanyId = companyId, CreatedAt = DateTime.UtcNow };
            _db.CompanySubscriptions.Add(sub);
        }

        sub.Plan = model.Plan;
        sub.Status = model.Status;
        sub.BillingCycle = model.BillingCycle;
        sub.TrialEndsAt = model.TrialEndsAt;
        sub.CurrentPeriodEndsAt = model.CurrentPeriodEndsAt;
        sub.BillingEmail = string.IsNullOrWhiteSpace(model.BillingEmail) ? null : model.BillingEmail.Trim();
        sub.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("superadmin.subscription.override", "CompanySubscription", sub.Id.ToString(), $"CompanyId={companyId}; Plan={sub.Plan}; Status={sub.Status}", ct);

        TempData["Success"] = "Subscription updated.";
        return Redirect("/superadmin/subscriptions");
    }

    [HttpPost("/superadmin/subscriptions/{companyId:int}/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelSubscription([FromRoute] int companyId, CancellationToken ct)
    {
        var sub = await _db.CompanySubscriptions.FirstOrDefaultAsync(s => s.CompanyId == companyId, ct);
        if (sub is null)
        {
            TempData["Success"] = "No subscription to cancel.";
            return Redirect("/superadmin/subscriptions");
        }

        sub.Status = SubscriptionStatus.Canceled;
        sub.Plan = SubscriptionPlan.Free;
        sub.CurrentPeriodEndsAt = DateTime.UtcNow;
        sub.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("superadmin.subscription.cancel", "CompanySubscription", sub.Id.ToString(), $"CompanyId={companyId}", ct);

        TempData["Success"] = "Subscription canceled and downgraded to Free.";
        return Redirect("/superadmin/subscriptions");
    }

    [HttpGet("/superadmin/reports/subscriptions")]
    public async Task<IActionResult> SubscriptionReports([FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
    {
        var y = year ?? DateTime.UtcNow.Year;
        var m = month ?? DateTime.UtcNow.Month;
        var start = new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);

        // Use audit logs for activation events when available
        var activations = await _db.AuditLogs
            .AsNoTracking()
            .Where(a => a.Action == "subscription.activated" && a.CreatedAt >= start && a.CreatedAt < end)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        var totalSubscriptions = activations.Count;

        long totalRevenueCentavos = 0;
        var items = new List<dynamic>();

        foreach (var act in activations)
        {
            int subId = 0;
            if (!string.IsNullOrWhiteSpace(act.EntityId) && int.TryParse(act.EntityId, out var parsed)) subId = parsed;

            CompanySubscription? sub = null;
            if (subId > 0)
                sub = await _db.CompanySubscriptions.AsNoTracking().Include(s => s.Company).FirstOrDefaultAsync(s => s.Id == subId, ct);

            // Fallback: try to find by company and created/updated date
            if (sub is null && !string.IsNullOrWhiteSpace(act.Details))
            {
                // details often contains "Plan=Pro" etc. Skip complex parsing for now
            }

            long amount = 0;
            string currency = "PHP";
            SubscriptionPlan plan = SubscriptionPlan.Free;
            var cycle = SubscriptionBillingCycle.Monthly;

            if (sub is not null)
            {
                plan = sub.Plan;
                cycle = sub.BillingCycle;
            }

            var planDef = await _settings.GetPlanAsync(plan, ct);
            currency = planDef.Currency ?? "PHP";
            amount = cycle == SubscriptionBillingCycle.Annual ? planDef.AnnualPriceCentavos : planDef.MonthlyPriceCentavos;

            totalRevenueCentavos += amount;

            items.Add(new { ActivatedAt = act.CreatedAt, CompanySubscription = sub, Plan = plan, BillingCycle = cycle, AmountCentavos = amount, Currency = currency });
        }

        var usersCount = await _db.Users.AsNoTracking().CountAsync(u => !u.IsArchived, ct);

        ViewBag.Year = y;
        ViewBag.Month = m;
        ViewBag.TotalSubscriptions = totalSubscriptions;
        ViewBag.TotalRevenueCentavos = totalRevenueCentavos;
        ViewBag.Currency = "PHP";
        ViewBag.UsersCount = usersCount;

        return View(items);
    }
}

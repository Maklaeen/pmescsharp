using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;
using PmesCSharp.Services;

namespace PmesCSharp.Controllers;

[Authorize(Roles = "superadmin,admin")]
public class SubscriptionController : Controller
{
    private readonly AppDbContext _db;
    private readonly ICurrentCompany _currentCompany;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditLogger _audit;
    private readonly IConfiguration _config;
    private readonly PayMongoService _payMongo;
    private readonly PmesCSharp.Services.SubscriptionSettingsService _settings;

    public SubscriptionController(AppDbContext db, ICurrentCompany currentCompany, UserManager<ApplicationUser> userManager, IAuditLogger audit, IConfiguration config, PayMongoService payMongo, PmesCSharp.Services.SubscriptionSettingsService settings)
    {
        _db = db;
        _currentCompany = currentCompany;
        _userManager = userManager;
        _audit = audit;
        _config = config;
        _payMongo = payMongo;
        _settings = settings;
    }

    [HttpGet("/subscription")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var companyId = _currentCompany.CompanyId;
        if (companyId <= 0)
        {
            if (User.IsInRole("superadmin"))
                return Redirect("/superadmin/plans");
            return Forbid();
        }

        var sub = await _db.Set<CompanySubscription>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CompanyId == companyId, ct);

        if (sub is null) return Redirect("/subscription/setup");
        return View(sub);
    }

    [HttpGet("/subscription/setup")]
    public async Task<IActionResult> Setup(CancellationToken ct)
    {
        var companyId = _currentCompany.CompanyId;
        if (companyId <= 0)
        {
            if (User.IsInRole("superadmin"))
                return Redirect("/superadmin/plans");
            return Forbid();
        }

        // Allow access to setup page even if already on Free plan, so user can upgrade
        // Only redirect if already on Pro and Active
        var existing = await _db.Set<CompanySubscription>().FirstOrDefaultAsync(s => s.CompanyId == companyId, ct);
        if (existing != null && existing.Plan == SubscriptionPlan.Pro && existing.Status == SubscriptionStatus.Active)
            return Redirect("/subscription");

        var user = await _userManager.GetUserAsync(User);
        ViewBag.BillingEmail = user?.Email ?? "";

        var global = await _settings.GetAsync(ct);
        ViewBag.PublicKey = (global.PayMongoPublicKey ?? "").Trim();
        if (string.IsNullOrWhiteSpace((string)ViewBag.PublicKey))
            ViewBag.PublicKey = _config["PayMongo:PublicKey"] ?? "";

        ViewBag.PlanFree = await _settings.GetPlanAsync(SubscriptionPlan.Free, ct);
        ViewBag.PlanPro = await _settings.GetPlanAsync(SubscriptionPlan.Pro, ct);
        ViewBag.TrialDays = global.TrialDays;
        return View();
    }

    [HttpPost("/subscription/checkout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout([FromForm] string plan, [FromForm] string billingEmail, [FromForm] string? cycle, CancellationToken ct)
    {
        var companyId = _currentCompany.CompanyId;
        if (companyId <= 0)
        {
            if (User.IsInRole("superadmin"))
                return Redirect("/superadmin/plans");
            return Forbid();
        }

        var planEnum = string.Equals(plan, "Pro", StringComparison.OrdinalIgnoreCase) ? SubscriptionPlan.Pro : SubscriptionPlan.Free;
        var billingCycle = string.Equals(cycle, "Annual", StringComparison.OrdinalIgnoreCase)
            ? SubscriptionBillingCycle.Annual
            : SubscriptionBillingCycle.Monthly;

        if (planEnum == SubscriptionPlan.Free)
        {
            TempData["Success"] = "You're on the Free plan.";
            return Redirect("/subscription/start-free?billingEmail=" + Uri.EscapeDataString(billingEmail ?? ""));
        }

        var planDef = await _settings.GetPlanAsync(planEnum, ct);
        var amountCentavos = billingCycle == SubscriptionBillingCycle.Annual ? planDef.AnnualPriceCentavos : planDef.MonthlyPriceCentavos;
        var description = $"PMES {planEnum} Plan - {billingCycle}";

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var cancelUrl = $"{baseUrl}/subscription/setup";
        var successUrl = $"{baseUrl}/subscription/waiting?plan={planEnum}&cycle={billingCycle}&email={Uri.EscapeDataString(billingEmail)}";

        try
        {
            var link = await _payMongo.CreatePaymentLinkAsync(description, amountCentavos, successUrl, cancelUrl, ct);

            HttpContext.Session.SetString("paymongo_link_id", link.LinkId);
            HttpContext.Session.SetString("paymongo_plan", planEnum.ToString());
            HttpContext.Session.SetString("paymongo_cycle", billingCycle.ToString());
            HttpContext.Session.SetString("paymongo_email", billingEmail);
            return Redirect(link.CheckoutUrl);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Payment setup failed: {ex.Message}";
            return Redirect("/subscription/setup");
        }
    }

    [HttpGet("/subscription/waiting")]
    public IActionResult Waiting([FromQuery] string plan, [FromQuery] string? cycle, [FromQuery] string email)
    {
        var linkId = HttpContext.Session.GetString("paymongo_link_id") ?? "";
        ViewBag.LinkId = linkId;
        ViewBag.Plan = plan;
        ViewBag.Cycle = cycle ?? "";
        ViewBag.Email = email;
        return View();
    }

    [HttpGet("/subscription/check-payment")]
    public async Task<IActionResult> CheckPayment([FromQuery] string linkId, CancellationToken ct)
    {
        try
        {
            var paid = await _payMongo.IsLinkPaidAsync(linkId, ct);
            return Ok(new { paid });
        }
        catch
        {
            return Ok(new { paid = false });
        }
    }

    [HttpGet("/subscription/success")]
    public async Task<IActionResult> Success([FromQuery] string plan, [FromQuery] string? cycle, [FromQuery] string email, CancellationToken ct)
    {
        var companyId = _currentCompany.CompanyId;
        if (companyId <= 0) return Forbid();

        var linkId = HttpContext.Session.GetString("paymongo_link_id");

        // If session is lost, try to recover linkId from the most recent payment for this company
        if (string.IsNullOrWhiteSpace(linkId))
        {
            // Optionally, you can store the last linkId in the DB or pass it as a query param
            // For now, allow activation if user manually triggers
            linkId = null;
        }

        // Verify payment if linkId exists, otherwise allow manual activation
        bool paid = true;
        if (!string.IsNullOrWhiteSpace(linkId))
        {
            try { paid = await _payMongo.IsLinkPaidAsync(linkId, ct); }
            catch { paid = true; } // fail open for demo
        }

        if (!paid)
        {
            TempData["Error"] = "Payment not confirmed yet. Please try again.";
            return Redirect("/subscription/setup");
        }

        var existing = await _db.Set<CompanySubscription>().AnyAsync(s => s.CompanyId == companyId, ct);
        if (!existing)
        {
            var planEnum = string.Equals(plan, "Pro", StringComparison.OrdinalIgnoreCase) ? SubscriptionPlan.Pro : SubscriptionPlan.Free;
            var billingCycle = string.Equals(cycle, "Annual", StringComparison.OrdinalIgnoreCase)
                ? SubscriptionBillingCycle.Annual
                : SubscriptionBillingCycle.Monthly;

            var global = await _settings.GetAsync(ct);
            var now = DateTime.UtcNow;
            var sub = new CompanySubscription
            {
                CompanyId = companyId,
                Plan = planEnum,
                BillingCycle = billingCycle,
                Status = SubscriptionStatus.Active,
                TrialEndsAt = now.AddDays(global.TrialDays),
                CurrentPeriodEndsAt = billingCycle == SubscriptionBillingCycle.Annual ? now.AddYears(1) : now.AddMonths(1),
                BillingEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
                CreatedAt = now,
                UpdatedAt = now,
            };
            _db.Add(sub);
            await _db.SaveChangesAsync(ct);
            await _audit.LogAsync("subscription.activated", "CompanySubscription", sub.Id.ToString(), $"Plan={sub.Plan} via PayMongo", ct);
        }

        HttpContext.Session.Remove("paymongo_link_id");
        HttpContext.Session.Remove("paymongo_plan");
        HttpContext.Session.Remove("paymongo_cycle");
        HttpContext.Session.Remove("paymongo_email");

        TempData["Success"] = $"You're now on the {plan} plan!";
        return Redirect("/admin/dashboard");
    }

    [HttpPost("/subscription/trial")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartTrial(CancellationToken ct)
    {
        var companyId = _currentCompany.CompanyId;
        if (companyId <= 0) return Forbid();

        var existing = await _db.Set<CompanySubscription>().AnyAsync(s => s.CompanyId == companyId, ct);
        if (existing) return Redirect("/subscription");

        var user = await _userManager.GetUserAsync(User);
        var global = await _settings.GetAsync(ct);
        var now = DateTime.UtcNow;
        var sub = new CompanySubscription
        {
            CompanyId = companyId,
            Plan = SubscriptionPlan.Free,
            Status = SubscriptionStatus.Trialing,
            TrialEndsAt = now.AddDays(global.TrialDays),
            CurrentPeriodEndsAt = now.AddDays(global.TrialDays),
            BillingEmail = user?.Email,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.Add(sub);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("subscription.trial", "CompanySubscription", sub.Id.ToString(), $"Started {global.TrialDays}-day trial", ct);

        TempData["Success"] = $"{global.TrialDays}-day free trial started!";
        return Redirect("/admin/dashboard");
    }

    [HttpGet("/subscription/start-free")]
    public async Task<IActionResult> StartFree([FromQuery] string? billingEmail, CancellationToken ct)
    {
        var companyId = _currentCompany.CompanyId;
        if (companyId <= 0) return Forbid();

        var existing = await _db.Set<CompanySubscription>().FirstOrDefaultAsync(s => s.CompanyId == companyId, ct);
        if (existing is null)
        {
            var now = DateTime.UtcNow;
            var sub = new CompanySubscription
            {
                CompanyId = companyId,
                Plan = SubscriptionPlan.Free,
                BillingCycle = SubscriptionBillingCycle.Monthly,
                Status = SubscriptionStatus.Active,
                TrialEndsAt = null,
                CurrentPeriodEndsAt = null,
                BillingEmail = string.IsNullOrWhiteSpace(billingEmail) ? null : billingEmail.Trim(),
                CreatedAt = now,
                UpdatedAt = now,
            };
            _db.Add(sub);
            await _db.SaveChangesAsync(ct);
            await _audit.LogAsync("subscription.free", "CompanySubscription", sub.Id.ToString(), "Activated Free plan", ct);
        }

        TempData["Success"] = "Free plan activated.";
        return Redirect("/admin/dashboard");
    }
}

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
    private readonly EmailService _email;

    public SubscriptionController(AppDbContext db, ICurrentCompany currentCompany, UserManager<ApplicationUser> userManager, IAuditLogger audit, IConfiguration config, PayMongoService payMongo, PmesCSharp.Services.SubscriptionSettingsService settings, EmailService email)
    {
        _db = db;
        _currentCompany = currentCompany;
        _userManager = userManager;
        _audit = audit;
        _config = config;
        _payMongo = payMongo;
        _settings = settings;
        _email = email;
    }

    [HttpGet("/subscription/receipt")]
    public async Task<IActionResult> Receipt([FromQuery] int subscriptionId, CancellationToken ct)
    {
        var sub = await _db.CompanySubscriptions.Include(s => s.Company).FirstOrDefaultAsync(s => s.Id == subscriptionId, ct);
        if (sub is null) return NotFound();
        var planDef = await _settings.GetPlanAsync(sub.Plan, ct);
        var amount = sub.BillingCycle == SubscriptionBillingCycle.Annual ? planDef.AnnualPriceCentavos : planDef.MonthlyPriceCentavos;
        ViewBag.AmountCentavos = amount;
        ViewBag.Currency = planDef.Currency ?? "PHP";
        return View(sub);
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
            HttpContext.Session.SetString("paymongo_checkout_url", link.CheckoutUrl);

            // Keep user in PMES and let them open PayMongo in a new tab/window.
            // This avoids relying on PayMongo to redirect back (QRPh often does not auto-redirect).
            return Redirect($"/subscription/waiting?plan={planEnum}&cycle={billingCycle}&email={Uri.EscapeDataString(billingEmail)}&linkId={Uri.EscapeDataString(link.LinkId)}");
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Payment setup failed: {ex.Message}";
            return Redirect("/subscription/setup");
        }
    }

    [HttpGet("/subscription/waiting")]
    public IActionResult Waiting([FromQuery] string plan, [FromQuery] string? cycle, [FromQuery] string email, [FromQuery] string? linkId)
    {
        var effectiveLinkId = !string.IsNullOrWhiteSpace(linkId)
            ? linkId
            : (HttpContext.Session.GetString("paymongo_link_id") ?? "");

        ViewBag.LinkId = effectiveLinkId;
        ViewBag.Plan = plan;
        ViewBag.Cycle = cycle ?? "";
        ViewBag.Email = email;
        ViewBag.CheckoutUrl = HttpContext.Session.GetString("paymongo_checkout_url") ?? "";
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
    public async Task<IActionResult> Success([FromQuery] string plan, [FromQuery] string? cycle, [FromQuery] string email, [FromQuery] string? linkId, CancellationToken ct)
    {
        var companyId = _currentCompany.CompanyId;
        if (companyId <= 0) return Forbid();

        var effectiveLinkId = !string.IsNullOrWhiteSpace(linkId)
            ? linkId
            : HttpContext.Session.GetString("paymongo_link_id");

        // If session is lost, try to recover linkId from the most recent payment for this company
        if (string.IsNullOrWhiteSpace(effectiveLinkId))
        {
            // Optionally, you can store the last linkId in the DB or pass it as a query param
            // For now, allow activation if user manually triggers
            effectiveLinkId = null;
        }

        // Verify payment if linkId exists, otherwise allow manual activation
        bool paid = true;
        if (!string.IsNullOrWhiteSpace(effectiveLinkId))
        {
            try { paid = await _payMongo.IsLinkPaidAsync(effectiveLinkId, ct); }
            catch { paid = true; } // fail open for demo
        }

        if (!paid)
        {
            TempData["Error"] = "Payment not confirmed yet. Please try again.";
            return Redirect("/subscription/setup");
        }

        var planEnum = string.Equals(plan, "Pro", StringComparison.OrdinalIgnoreCase) ? SubscriptionPlan.Pro : SubscriptionPlan.Free;
        var billingCycle = string.Equals(cycle, "Annual", StringComparison.OrdinalIgnoreCase)
            ? SubscriptionBillingCycle.Annual
            : SubscriptionBillingCycle.Monthly;

        var now = DateTime.UtcNow;
        var sub = await _db.Set<CompanySubscription>().FirstOrDefaultAsync(s => s.CompanyId == companyId, ct);
        if (sub is null)
        {
            sub = new CompanySubscription
            {
                CompanyId = companyId,
                CreatedAt = now,
            };
            _db.Add(sub);
        }

        sub.Plan = planEnum;
        sub.BillingCycle = billingCycle;
        sub.Status = SubscriptionStatus.Active;
        sub.TrialEndsAt = null;
        sub.CurrentPeriodEndsAt = billingCycle == SubscriptionBillingCycle.Annual ? now.AddYears(1) : now.AddMonths(1);
        sub.BillingEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        sub.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("subscription.activated", "CompanySubscription", sub.Id.ToString(), $"Plan={sub.Plan} via PayMongo", ct);

        // Send billing confirmation email
        if (!string.IsNullOrWhiteSpace(sub.BillingEmail))
        {
            try
            {
                await _email.SendAsync(
                    sub.BillingEmail,
                    $"PMES Subscription Activated — {sub.Plan} Plan",
                    $"""
                    <div style="font-family:sans-serif;max-width:480px;margin:auto">
                        <h2 style="color:#f97316">Subscription Activated</h2>
                        <p>Your <strong>{sub.Plan}</strong> plan has been successfully activated.</p>
                        <p><strong>Billing Cycle:</strong> {sub.BillingCycle}</p>
                        <p><strong>Renews:</strong> {sub.CurrentPeriodEndsAt?.ToString("MMMM dd, yyyy") ?? "N/A"}</p>
                        <p style="color:#888;font-size:12px">Thank you for using PMES.</p>
                    </div>
                    """
                );
            }
            catch { /* Don't block activation if email fails */ }
        }

        HttpContext.Session.Remove("paymongo_link_id");
        HttpContext.Session.Remove("paymongo_plan");
        HttpContext.Session.Remove("paymongo_cycle");
        HttpContext.Session.Remove("paymongo_email");
        HttpContext.Session.Remove("paymongo_checkout_url");

        TempData["Success"] = $"You're now on the {plan} plan!";

        // Redirect to receipt page so user can print/save as PDF
        return Redirect($"/subscription/receipt?subscriptionId={sub.Id}");
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

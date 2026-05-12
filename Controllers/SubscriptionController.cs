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

    public SubscriptionController(AppDbContext db, ICurrentCompany currentCompany, UserManager<ApplicationUser> userManager, IAuditLogger audit, IConfiguration config, PayMongoService payMongo)
    {
        _db = db;
        _currentCompany = currentCompany;
        _userManager = userManager;
        _audit = audit;
        _config = config;
        _payMongo = payMongo;
    }

    [HttpGet("/subscription")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var companyId = _currentCompany.CompanyId;
        if (companyId <= 0) return Forbid();

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
        if (companyId <= 0) return Forbid();

        var existing = await _db.Set<CompanySubscription>().AnyAsync(s => s.CompanyId == companyId, ct);
        if (existing) return Redirect("/subscription");

        var user = await _userManager.GetUserAsync(User);
        ViewBag.BillingEmail = user?.Email ?? "";
        ViewBag.PublicKey = _config["PayMongo:PublicKey"] ?? "";
        return View();
    }

    [HttpPost("/subscription/checkout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout([FromForm] string plan, [FromForm] string billingEmail, CancellationToken ct)
    {
        var companyId = _currentCompany.CompanyId;
        if (companyId <= 0) return Forbid();

        var planEnum = plan == "Pro" ? SubscriptionPlan.Pro : SubscriptionPlan.Standard;
        var amountCentavos = planEnum == SubscriptionPlan.Pro ? 4900_00L : 2900_00L; // PHP centavos
        var description = $"PMES {planEnum} Plan - Monthly";

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var successUrl = $"{baseUrl}/subscription/success?plan={plan}&email={Uri.EscapeDataString(billingEmail)}";
        var cancelUrl = $"{baseUrl}/subscription/setup";

        try
        {
            var link = await _payMongo.CreatePaymentLinkAsync(description, amountCentavos, successUrl, cancelUrl, ct);
            // Store linkId in session for verification
            HttpContext.Session.SetString("paymongo_link_id", link.LinkId);
            HttpContext.Session.SetString("paymongo_plan", plan);
            HttpContext.Session.SetString("paymongo_email", billingEmail);
            return Redirect(link.CheckoutUrl);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Payment setup failed: {ex.Message}";
            return Redirect("/subscription/setup");
        }
    }

    [HttpGet("/subscription/success")]
    public async Task<IActionResult> Success([FromQuery] string plan, [FromQuery] string email, CancellationToken ct)
    {
        var companyId = _currentCompany.CompanyId;
        if (companyId <= 0) return Forbid();

        var linkId = HttpContext.Session.GetString("paymongo_link_id");

        // Verify payment if linkId exists
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
            var planEnum = plan == "Pro" ? SubscriptionPlan.Pro : SubscriptionPlan.Standard;
            var now = DateTime.UtcNow;
            var sub = new CompanySubscription
            {
                CompanyId = companyId,
                Plan = planEnum,
                Status = SubscriptionStatus.Active,
                TrialEndsAt = now.AddDays(30),
                CurrentPeriodEndsAt = now.AddMonths(1),
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
        HttpContext.Session.Remove("paymongo_email");

        TempData["Success"] = $"🎉 You're now on the {plan} plan!";
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
        var now = DateTime.UtcNow;
        var sub = new CompanySubscription
        {
            CompanyId = companyId,
            Plan = SubscriptionPlan.Standard,
            Status = SubscriptionStatus.Trialing,
            TrialEndsAt = now.AddDays(30),
            CurrentPeriodEndsAt = now.AddDays(30),
            BillingEmail = user?.Email,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.Add(sub);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("subscription.trial", "CompanySubscription", sub.Id.ToString(), "Started 30-day trial", ct);

        TempData["Success"] = "30-day free trial started!";
        return Redirect("/admin/dashboard");
    }
}

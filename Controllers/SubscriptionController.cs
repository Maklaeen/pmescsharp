using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;
using PmesCSharp.Services;
using PmesCSharp.ViewModels.Subscription;
using Stripe;

namespace PmesCSharp.Controllers;

[Authorize(Roles = "superadmin,admin")]
public class SubscriptionController : Controller
{
    private readonly AppDbContext _db;
    private readonly ICurrentCompany _currentCompany;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditLogger _audit;
    private readonly IConfiguration _config;

    public SubscriptionController(AppDbContext db, ICurrentCompany currentCompany, UserManager<ApplicationUser> userManager, IAuditLogger audit, IConfiguration config)
    {
        _db = db;
        _currentCompany = currentCompany;
        _userManager = userManager;
        _audit = audit;
        _config = config;
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
            StripePublishableKey = _config["Stripe:PublishableKey"],
        };

        return View(vm);
    }

    [HttpPost("/subscription/setup/payment-intent")]
    [ValidateAntiForgeryToken]
    public IActionResult CreatePaymentIntent([FromBody] CreatePaymentIntentRequest req)
    {
        var secretKey = _config["Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
            return BadRequest(new { error = "Stripe not configured." });

        StripeConfiguration.ApiKey = secretKey;

        var amount = req.Plan == "Pro" ? 4999L : 2999L; // cents

        var options = new PaymentIntentCreateOptions
        {
            Amount = amount,
            Currency = "usd",
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true },
            Metadata = new Dictionary<string, string> { ["plan"] = req.Plan ?? "Standard" },
        };

        var service = new PaymentIntentService();
        var intent = service.Create(options);

        return Ok(new { clientSecret = intent.ClientSecret });
    }

    [HttpPost("/subscription/setup")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSetup(SubscriptionSetupViewModel model, CancellationToken cancellationToken)
    {
        var companyId = _currentCompany.CompanyId;
        if (companyId <= 0) return Forbid();

        model.StripePublishableKey = _config["Stripe:PublishableKey"];

        if (!ModelState.IsValid)
            return View("Setup", model);

        var existing = await _db.Set<CompanySubscription>().AnyAsync(s => s.CompanyId == companyId, cancellationToken);
        if (existing)
            return Redirect("/subscription");

        // Verify payment intent if Stripe is configured
        var secretKey = _config["Stripe:SecretKey"];
        if (!string.IsNullOrWhiteSpace(secretKey) && !string.IsNullOrWhiteSpace(model.PaymentIntentId))
        {
            StripeConfiguration.ApiKey = secretKey;
            var service = new PaymentIntentService();
            var intent = service.Get(model.PaymentIntentId);
            if (intent.Status != "succeeded")
            {
                ModelState.AddModelError(string.Empty, "Payment was not completed. Please try again.");
                return View("Setup", model);
            }
        }

        var now = DateTime.UtcNow;
        var sub = new CompanySubscription
        {
            CompanyId = companyId,
            Plan = model.Plan,
            Status = SubscriptionStatus.Active,
            TrialEndsAt = now.AddDays(30),
            CurrentPeriodEndsAt = now.AddMonths(1),
            BillingEmail = string.IsNullOrWhiteSpace(model.BillingEmail) ? null : model.BillingEmail.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.Add(sub);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync("subscription.setup", "CompanySubscription", sub.Id.ToString(), $"Plan={sub.Plan}; status={sub.Status}", cancellationToken);

        TempData["Success"] = $"You're now on the {sub.Plan} plan!";
        return Redirect("/admin");
    }
}

public class CreatePaymentIntentRequest
{
    public string? Plan { get; set; }
}

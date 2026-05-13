using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;

namespace PmesCSharp.Services;

public sealed class EntitlementService
{
    private readonly AppDbContext _db;
    private readonly ICurrentCompany _currentCompany;
    private readonly SubscriptionSettingsService _settings;

    public EntitlementService(AppDbContext db, ICurrentCompany currentCompany, SubscriptionSettingsService settings)
    {
        _db = db;
        _currentCompany = currentCompany;
        _settings = settings;
    }

    public async Task<CompanyEntitlements> GetCurrentCompanyEntitlementsAsync(CancellationToken ct = default)
    {
        var companyId = _currentCompany.CompanyId;
        if (companyId <= 0)
        {
            var freePlan = await _settings.GetPlanAsync(SubscriptionPlan.Free, ct);
            return CompanyEntitlements.FromPlan(SubscriptionPlan.Free, SubscriptionStatus.Expired, freePlan);
        }

        var sub = await _db.CompanySubscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CompanyId == companyId, ct);

        var global = await _settings.GetAsync(ct);

        if (sub is null)
        {
            var freePlan = await _settings.GetPlanAsync(SubscriptionPlan.Free, ct);
            return CompanyEntitlements.FromPlan(SubscriptionPlan.Free, SubscriptionStatus.Expired, freePlan);
        }

        var now = DateTime.UtcNow;
        var graceUntil = sub.CurrentPeriodEndsAt?.AddDays(global.GracePeriodDays);
        var trialUntil = sub.TrialEndsAt?.AddDays(global.GracePeriodDays);

        var isActive = sub.Status == SubscriptionStatus.Active
            && (sub.CurrentPeriodEndsAt is null || now <= graceUntil);

        var isTrial = sub.Status == SubscriptionStatus.Trialing
            && (sub.TrialEndsAt is null || now <= trialUntil);

        var effectivePlan = (isActive || isTrial) ? sub.Plan : SubscriptionPlan.Free;

        var planDef = await _settings.GetPlanAsync(effectivePlan, ct);
        return CompanyEntitlements.FromPlan(effectivePlan, sub.Status, planDef);
    }

    public async Task<LimitCheckResult> EnsureCanCreateProductAsync(CancellationToken ct = default)
    {
        var ent = await GetCurrentCompanyEntitlementsAsync(ct);
        if (ent.MaxProducts <= 0) return LimitCheckResult.AllowedResult();

        var count = await _db.Products.CountAsync(ct);
        return count >= ent.MaxProducts
            ? LimitCheckResult.Blocked($"Product limit reached ({count}/{ent.MaxProducts}). Upgrade to Pro to add more products.")
            : LimitCheckResult.AllowedResult();
    }

    public async Task<LimitCheckResult> EnsureCanCreateMaterialAsync(CancellationToken ct = default)
    {
        var ent = await GetCurrentCompanyEntitlementsAsync(ct);
        if (ent.MaxMaterials <= 0) return LimitCheckResult.AllowedResult();

        var count = await _db.Materials.CountAsync(ct);
        return count >= ent.MaxMaterials
            ? LimitCheckResult.Blocked($"Material limit reached ({count}/{ent.MaxMaterials}). Upgrade to Pro to add more materials.")
            : LimitCheckResult.AllowedResult();
    }

    public async Task<LimitCheckResult> EnsureCanApproveMoreUsersAsync(UserManager<ApplicationUser> userManager, CancellationToken ct = default)
    {
        var ent = await GetCurrentCompanyEntitlementsAsync(ct);
        if (ent.MaxUsers <= 0) return LimitCheckResult.AllowedResult();

        var companyId = _currentCompany.CompanyId;
        if (companyId <= 0) return LimitCheckResult.AllowedResult();

        var approvedCount = await userManager.Users.CountAsync(u => u.CompanyId == companyId && u.IsApproved && !u.IsArchived, ct);
        return approvedCount >= ent.MaxUsers
            ? LimitCheckResult.Blocked($"User limit reached ({approvedCount}/{ent.MaxUsers}). You can still invite users, but you cannot approve more until you upgrade.")
            : LimitCheckResult.AllowedResult();
    }

    public async Task<LimitCheckResult> EnsureCanGenerateMoreWorkOrdersThisMonthAsync(CancellationToken ct = default)
    {
        var ent = await GetCurrentCompanyEntitlementsAsync(ct);
        if (ent.MaxWorkOrdersPerMonth <= 0) return LimitCheckResult.AllowedResult();

        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);

        var count = await _db.WorkOrders.CountAsync(w => w.CreatedAt >= start && w.CreatedAt < end, ct);
        return count >= ent.MaxWorkOrdersPerMonth
            ? LimitCheckResult.Blocked($"Work order monthly limit reached ({count}/{ent.MaxWorkOrdersPerMonth}). Upgrade to Pro to increase limits.")
            : LimitCheckResult.AllowedResult();
    }
}

public sealed record CompanyEntitlements(
    SubscriptionPlan Plan,
    SubscriptionStatus Status,
    int MaxUsers,
    int MaxProducts,
    int MaxMaterials,
    int MaxWorkOrdersPerMonth,
    int MaxStorageMb,
    bool EnableReports,
    bool EnableCosting,
    bool EnableAuditLogs)
{
    public static CompanyEntitlements FromPlan(SubscriptionPlan plan, SubscriptionStatus status, SubscriptionPlanDefinition def)
        => new(
            plan,
            status,
            def.MaxUsers,
            def.MaxProducts,
            def.MaxMaterials,
            def.MaxWorkOrdersPerMonth,
            def.MaxStorageMb,
            def.EnableReports,
            def.EnableCosting,
            def.EnableAuditLogs);
}

public sealed record LimitCheckResult(bool Allowed, string? Message)
{
    public static LimitCheckResult AllowedResult() => new(true, null);
    public static LimitCheckResult Blocked(string message) => new(false, message);
}

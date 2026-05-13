using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;

namespace PmesCSharp.Services;

public sealed class SubscriptionSettingsService
{
    private readonly AppDbContext _db;

    public SubscriptionSettingsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<SubscriptionGlobalSetting> GetAsync(CancellationToken ct = default)
    {
        var row = await _db.SubscriptionGlobalSettings.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
        return row ?? new SubscriptionGlobalSetting();
    }

    public async Task<SubscriptionPlanDefinition> GetPlanAsync(SubscriptionPlan plan, CancellationToken ct = default)
    {
        var row = await _db.SubscriptionPlanDefinitions.AsNoTracking().FirstOrDefaultAsync(p => p.Plan == plan, ct);
        if (row is not null) return row;

        // Safe defaults if not seeded yet
        if (plan == SubscriptionPlan.Pro)
        {
            var monthly = 4900_00;
            var annual = (int)Math.Round(monthly * 12 * 0.9); // 10% discount
            return new SubscriptionPlanDefinition
            {
                Plan = SubscriptionPlan.Pro,
                Currency = "PHP",
                MonthlyPriceCentavos = monthly,
                AnnualPriceCentavos = annual,
                MaxUsers = 0,
                MaxProducts = 0,
                MaxMaterials = 0,
                MaxWorkOrdersPerMonth = 0,
                MaxStorageMb = 0,
                EnableReports = true,
                EnableCosting = true,
                EnableAuditLogs = true,
            };
        }
        else
        {
            return new SubscriptionPlanDefinition
            {
                Plan = SubscriptionPlan.Free,
                Currency = "PHP",
                MonthlyPriceCentavos = 0,
                AnnualPriceCentavos = 0,
                MaxUsers = 300,
                MaxProducts = 5000,
                MaxMaterials = 5000,
                MaxWorkOrdersPerMonth = 0,
                MaxStorageMb = 0,
                EnableReports = true,
                EnableCosting = true,
                EnableAuditLogs = true,
            };
        }
    }
}

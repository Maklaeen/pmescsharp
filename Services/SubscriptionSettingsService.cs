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
        return plan == SubscriptionPlan.Pro
            ? new SubscriptionPlanDefinition
            {
                Plan = SubscriptionPlan.Pro,
                Currency = "PHP",
                MonthlyPriceCentavos = 4900_00,
                AnnualPriceCentavos = 49000_00,
                MaxUsers = 0,
                MaxProducts = 0,
                MaxMaterials = 0,
                MaxWorkOrdersPerMonth = 0,
                MaxStorageMb = 0,
                EnableReports = true,
                EnableCosting = true,
                EnableAuditLogs = true,
            }
            : new SubscriptionPlanDefinition
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

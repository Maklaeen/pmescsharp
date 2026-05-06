using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace PmesCSharp.Data;

public sealed class TenantEntityInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentCompany _currentCompany;

    public TenantEntityInterceptor(ICurrentCompany currentCompany)
    {
        _currentCompany = currentCompany;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyCompanyId(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyCompanyId(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyCompanyId(DbContext? context)
    {
        if (context is null) return;

        var companyId = _currentCompany.CompanyId;
        if (companyId == 0) return;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            SetCompanyIdIfPresent(entry, companyId);
        }
    }

    private static void SetCompanyIdIfPresent(EntityEntry entry, int companyId)
    {
        if (entry.State is not (EntityState.Added or EntityState.Modified))
            return;

        var prop = entry.Metadata.FindProperty("CompanyId");
        if (prop is null) return;

        // Only force-set on Added. For Modified, keep existing to avoid cross-tenant moves.
        if (entry.State == EntityState.Added)
        {
            entry.Property("CompanyId").CurrentValue = companyId;
        }
    }
}

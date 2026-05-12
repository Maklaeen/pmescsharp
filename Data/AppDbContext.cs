using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Models;

namespace PmesCSharp.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly ICurrentCompany? _currentCompany;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentCompany currentCompany) : base(options)
    {
        _currentCompany = currentCompany;
    }

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<CompanyInvite> CompanyInvites => Set<CompanyInvite>();

    public DbSet<CompanySubscription> CompanySubscriptions => Set<CompanySubscription>();

    public DbSet<CompanyProfile> CompanyProfiles => Set<CompanyProfile>();

    public DbSet<UserSetting> UserSettings => Set<UserSetting>();

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<BillOfMaterial> BillOfMaterials => Set<BillOfMaterial>();
    public DbSet<ProductionSchedule> ProductionSchedules => Set<ProductionSchedule>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<MaterialMovement> MaterialMovements => Set<MaterialMovement>();
    public DbSet<QualityCheck> QualityChecks => Set<QualityCheck>();
    public DbSet<ProductionCost> ProductionCosts => Set<ProductionCost>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Company>(e =>
        {
            e.HasIndex(c => c.Code).IsUnique();
        });

        // Multi-tenant filters (applies automatically when authenticated)
        modelBuilder.Entity<Product>().HasQueryFilter(e => _currentCompany == null || _currentCompany.CompanyId == 0 || e.CompanyId == _currentCompany.CompanyId);
        modelBuilder.Entity<Material>().HasQueryFilter(e => _currentCompany == null || _currentCompany.CompanyId == 0 || e.CompanyId == _currentCompany.CompanyId);
        modelBuilder.Entity<BillOfMaterial>().HasQueryFilter(e => _currentCompany == null || _currentCompany.CompanyId == 0 || e.CompanyId == _currentCompany.CompanyId);
        modelBuilder.Entity<ProductionSchedule>().HasQueryFilter(e => _currentCompany == null || _currentCompany.CompanyId == 0 || e.CompanyId == _currentCompany.CompanyId);
        modelBuilder.Entity<WorkOrder>().HasQueryFilter(e => _currentCompany == null || _currentCompany.CompanyId == 0 || e.CompanyId == _currentCompany.CompanyId);
        modelBuilder.Entity<MaterialMovement>().HasQueryFilter(e => _currentCompany == null || _currentCompany.CompanyId == 0 || e.CompanyId == _currentCompany.CompanyId);
        modelBuilder.Entity<QualityCheck>().HasQueryFilter(e => _currentCompany == null || _currentCompany.CompanyId == 0 || e.CompanyId == _currentCompany.CompanyId);
        modelBuilder.Entity<ProductionCost>().HasQueryFilter(e => _currentCompany == null || _currentCompany.CompanyId == 0 || e.CompanyId == _currentCompany.CompanyId);
        modelBuilder.Entity<AuditLog>().HasQueryFilter(e => _currentCompany == null || _currentCompany.CompanyId == 0 || e.CompanyId == _currentCompany.CompanyId);
       modelBuilder.Entity<CompanyInvite>().HasQueryFilter(e => _currentCompany == null || _currentCompany.CompanyId == 0 || e.CompanyId == _currentCompany.CompanyId);
       modelBuilder.Entity<CompanySubscription>().HasQueryFilter(e => _currentCompany == null || _currentCompany.CompanyId == 0 || e.CompanyId == _currentCompany.CompanyId);
        modelBuilder.Entity<CompanyProfile>().HasQueryFilter(e => _currentCompany == null || _currentCompany.CompanyId == 0 || e.CompanyId == _currentCompany.CompanyId);

        modelBuilder.Entity<Product>(e =>
        {
            e.HasIndex(p => new { p.CompanyId, p.ProductCode }).IsUnique();
            e.HasOne(p => p.Company)
                .WithMany()
                .HasForeignKey(p => p.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Material>(e =>
        {
            e.HasIndex(m => new { m.CompanyId, m.MaterialCode }).IsUnique();
            e.HasOne(m => m.Company)
                .WithMany()
                .HasForeignKey(m => m.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BillOfMaterial>(e =>
        {
            e.HasIndex(b => new { b.CompanyId, b.ProductId, b.MaterialId }).IsUnique();
            e.HasOne(b => b.Product).WithMany(p => p.BillOfMaterials).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(b => b.Material).WithMany().OnDelete(DeleteBehavior.Cascade);
            e.HasOne(b => b.Company).WithMany().HasForeignKey(b => b.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProductionSchedule>(e =>
        {
            e.HasIndex(s => new { s.Status, s.ScheduleDate });
            e.HasIndex(s => new { s.Status, s.ExpectedEndAt });
            e.HasIndex(s => new { s.CompanyId, s.Status, s.ScheduleDate });
            e.HasOne(s => s.Product).WithMany(p => p.ProductionSchedules).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.CreatedByUser).WithMany().HasForeignKey(s => s.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(s => s.Company).WithMany().HasForeignKey(s => s.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkOrder>(e =>
        {
            e.HasIndex(w => new { w.CompanyId, w.WorkOrderNo }).IsUnique();
            e.HasIndex(w => new { w.ProductionScheduleId, w.Status });
            e.HasIndex(w => new { w.AssignedToUserId, w.Status });
            e.HasOne(w => w.ProductionSchedule).WithMany(s => s.WorkOrders).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(w => w.AssignedToUser).WithMany().HasForeignKey(w => w.AssignedToUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(w => w.Company).WithMany().HasForeignKey(w => w.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MaterialMovement>(e =>
        {
            e.HasIndex(m => new { m.MaterialId, m.MovementType });
            e.HasIndex(m => m.ProductionScheduleId);
            e.HasIndex(m => m.WorkOrderId);
            e.HasOne(m => m.Material).WithMany().OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.ProductionSchedule).WithMany(s => s.MaterialMovements).HasForeignKey(m => m.ProductionScheduleId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(m => m.WorkOrder).WithMany(w => w.MaterialMovements).HasForeignKey(m => m.WorkOrderId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(m => m.CreatedByUser).WithMany().HasForeignKey(m => m.CreatedByUserId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(m => m.Company).WithMany().HasForeignKey(m => m.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<QualityCheck>(e =>
        {
            e.HasIndex(q => new { q.ProductionScheduleId, q.Result });
            e.HasOne(q => q.ProductionSchedule).WithMany(s => s.QualityChecks).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(q => q.InspectedByUser).WithMany().HasForeignKey(q => q.InspectedByUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(q => q.Company).WithMany().HasForeignKey(q => q.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProductionCost>(e =>
        {
            e.HasIndex(c => c.ProductionScheduleId).IsUnique();
            e.HasOne(c => c.ProductionSchedule).WithOne(s => s.ProductionCost).HasForeignKey<ProductionCost>(c => c.ProductionScheduleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.ComputedByUser).WithMany().HasForeignKey(c => c.ComputedByUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(c => c.Company).WithMany().HasForeignKey(c => c.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApplicationUser>(e =>
        {
            e.HasIndex(u => u.CompanyId);
            e.HasOne(u => u.Company).WithMany().HasForeignKey(u => u.CompanyId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasIndex(a => new { a.CompanyId, a.CreatedAt });
            e.HasOne(a => a.Company).WithMany().HasForeignKey(a => a.CompanyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.ActorUser).WithMany().HasForeignKey(a => a.ActorUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CompanyInvite>(e =>
        {
            e.HasIndex(i => i.TokenHash).IsUnique();
            e.HasIndex(i => new { i.CompanyId, i.InvitedEmail, i.CreatedAt });
            e.HasIndex(i => new { i.CompanyId, i.Code });
            e.HasOne(i => i.Company).WithMany().HasForeignKey(i => i.CompanyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(i => i.CreatedByUser).WithMany().HasForeignKey(i => i.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CompanySubscription>(e =>
        {
            e.HasIndex(s => s.CompanyId).IsUnique();
            e.HasOne(s => s.Company).WithMany().HasForeignKey(s => s.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CompanyProfile>(e =>
        {
            e.HasIndex(p => p.CompanyId).IsUnique();
            e.HasOne(p => p.Company).WithMany().HasForeignKey(p => p.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserSetting>(e =>
        {
            e.HasIndex(s => s.UserId).IsUnique();
            e.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

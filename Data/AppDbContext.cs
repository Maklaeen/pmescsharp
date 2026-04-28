using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Models;

namespace PmesCSharp.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<BillOfMaterial> BillOfMaterials => Set<BillOfMaterial>();
    public DbSet<ProductionSchedule> ProductionSchedules => Set<ProductionSchedule>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<MaterialMovement> MaterialMovements => Set<MaterialMovement>();
    public DbSet<QualityCheck> QualityChecks => Set<QualityCheck>();
    public DbSet<ProductionCost> ProductionCosts => Set<ProductionCost>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(e => e.HasIndex(p => p.ProductCode).IsUnique());
        modelBuilder.Entity<Material>(e => e.HasIndex(m => m.MaterialCode).IsUnique());

        modelBuilder.Entity<BillOfMaterial>(e =>
        {
            e.HasIndex(b => new { b.ProductId, b.MaterialId }).IsUnique();
            e.HasOne(b => b.Product).WithMany(p => p.BillOfMaterials).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(b => b.Material).WithMany().OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductionSchedule>(e =>
        {
            e.HasIndex(s => new { s.Status, s.ScheduleDate });
            e.HasIndex(s => new { s.Status, s.ExpectedEndAt });
            e.HasOne(s => s.Product).WithMany(p => p.ProductionSchedules).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.CreatedByUser).WithMany().HasForeignKey(s => s.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<WorkOrder>(e =>
        {
            e.HasIndex(w => w.WorkOrderNo).IsUnique();
            e.HasIndex(w => new { w.ProductionScheduleId, w.Status });
            e.HasIndex(w => new { w.AssignedToUserId, w.Status });
            e.HasOne(w => w.ProductionSchedule).WithMany(s => s.WorkOrders).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(w => w.AssignedToUser).WithMany().HasForeignKey(w => w.AssignedToUserId).OnDelete(DeleteBehavior.SetNull);
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
        });

        modelBuilder.Entity<QualityCheck>(e =>
        {
            e.HasIndex(q => new { q.ProductionScheduleId, q.Result });
            e.HasOne(q => q.ProductionSchedule).WithMany(s => s.QualityChecks).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(q => q.InspectedByUser).WithMany().HasForeignKey(q => q.InspectedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProductionCost>(e =>
        {
            e.HasIndex(c => c.ProductionScheduleId).IsUnique();
            e.HasOne(c => c.ProductionSchedule).WithOne(s => s.ProductionCost).HasForeignKey<ProductionCost>(c => c.ProductionScheduleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.ComputedByUser).WithMany().HasForeignKey(c => c.ComputedByUserId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}

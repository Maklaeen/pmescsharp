using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PmesCSharp.Models;

public class ProductionSchedule
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int PlannedQuantity { get; set; }

    public DateOnly ScheduleDate { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = "planned";

    public string? CreatedByUserId { get; set; }
    public ApplicationUser? CreatedByUser { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? ExpectedEndAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<WorkOrder> WorkOrders { get; set; } = [];
    public ICollection<MaterialMovement> MaterialMovements { get; set; } = [];
    public ICollection<QualityCheck> QualityChecks { get; set; } = [];
    public ProductionCost? ProductionCost { get; set; }
}

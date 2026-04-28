using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PmesCSharp.Models;

public class MaterialMovement
{
    public int Id { get; set; }

    public int MaterialId { get; set; }
    public Material Material { get; set; } = null!;

    [Required, StringLength(10)]
    public string MovementType { get; set; } = "in";

    [Column(TypeName = "decimal(10,4)")]
    public decimal Quantity { get; set; }

    [StringLength(20)]
    public string Unit { get; set; } = "pcs";

    [StringLength(30)]
    public string? ReferenceType { get; set; }

    public int? ProductionScheduleId { get; set; }
    public ProductionSchedule? ProductionSchedule { get; set; }

    public int? WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }

    public string? CreatedByUserId { get; set; }
    public ApplicationUser? CreatedByUser { get; set; }

    [StringLength(255)]
    public string? Remarks { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

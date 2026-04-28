using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PmesCSharp.Models;

public class WorkOrder
{
    public int Id { get; set; }

    public int ProductionScheduleId { get; set; }
    public ProductionSchedule ProductionSchedule { get; set; } = null!;

    [Required, StringLength(40)]
    public string WorkOrderNo { get; set; } = "";

    [Required, StringLength(50)]
    public string ProcessStep { get; set; } = "";

    public string? AssignedToUserId { get; set; }
    public ApplicationUser? AssignedToUser { get; set; }

    public int PlannedQty { get; set; }
    public int ActualQty { get; set; } = 0;

    [StringLength(20)]
    public string Status { get; set; } = "pending";

    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<MaterialMovement> MaterialMovements { get; set; } = [];
}

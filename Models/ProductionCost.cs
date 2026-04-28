using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PmesCSharp.Models;

public class ProductionCost
{
    public int Id { get; set; }

    public int ProductionScheduleId { get; set; }
    public ProductionSchedule ProductionSchedule { get; set; } = null!;

    [Column(TypeName = "decimal(12,2)")]
    public decimal MaterialCost { get; set; } = 0;

    [Column(TypeName = "decimal(12,2)")]
    public decimal LaborCost { get; set; } = 0;

    [Column(TypeName = "decimal(12,2)")]
    public decimal TotalCost { get; set; } = 0;

    [Column(TypeName = "decimal(12,4)")]
    public decimal CostPerUnit { get; set; } = 0;

    public string? ComputedByUserId { get; set; }
    public ApplicationUser? ComputedByUser { get; set; }

    public DateTime? ComputedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

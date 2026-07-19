using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PmesCSharp.Models;

public class QualityCheck
{
    public int Id { get; set; }

    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public int ProductionScheduleId { get; set; }
    public ProductionSchedule ProductionSchedule { get; set; } = null!;

    public string? InspectedByUserId { get; set; }
    public ApplicationUser? InspectedByUser { get; set; }

    [Required, StringLength(20)]
    public string Result { get; set; } = "passed";

    public int QtyPassed { get; set; } = 0;
    public int QtyFailed { get; set; } = 0;

    [StringLength(255)]
    public string? Remarks { get; set; }

    public DateTime? InspectedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

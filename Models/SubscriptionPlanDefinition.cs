using System.ComponentModel.DataAnnotations;

namespace PmesCSharp.Models;

public class SubscriptionPlanDefinition
{
    public int Id { get; set; }

    [Required]
    public SubscriptionPlan Plan { get; set; }

    [Required, StringLength(3)]
    public string Currency { get; set; } = "PHP";

    public long MonthlyPriceCentavos { get; set; }
    public long AnnualPriceCentavos { get; set; }

    public int MaxUsers { get; set; } = 300;
    public int MaxProducts { get; set; } = 5000;
    public int MaxMaterials { get; set; } = 5000;
    public int MaxWorkOrdersPerMonth { get; set; } = 0; // 0 = unlimited
    public int MaxStorageMb { get; set; } = 0; // 0 = unlimited

    public bool EnableReports { get; set; } = true;
    public bool EnableCosting { get; set; } = true;
    public bool EnableAuditLogs { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

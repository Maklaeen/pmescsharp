using System.ComponentModel.DataAnnotations;
using PmesCSharp.Models;

namespace PmesCSharp.ViewModels.SuperAdmin;

public class PlanEditViewModel
{
    [Required]
    public SubscriptionPlan Plan { get; set; }

    [Required, StringLength(3)]
    public string Currency { get; set; } = "PHP";

    [Range(0, long.MaxValue)]
    public long MonthlyPriceCentavos { get; set; }

    [Range(0, long.MaxValue)]
    public long AnnualPriceCentavos { get; set; }

    [Range(0, int.MaxValue)]
    public int MaxUsers { get; set; }

    [Range(0, int.MaxValue)]
    public int MaxProducts { get; set; }

    [Range(0, int.MaxValue)]
    public int MaxMaterials { get; set; }

    [Range(0, int.MaxValue)]
    public int MaxWorkOrdersPerMonth { get; set; }

    [Range(0, int.MaxValue)]
    public int MaxStorageMb { get; set; }

    public bool EnableReports { get; set; }
    public bool EnableCosting { get; set; }
    public bool EnableAuditLogs { get; set; }
}

using PmesCSharp.Models;

namespace PmesCSharp.ViewModels.SuperAdmin;

public class CompanySubscriptionListItemViewModel
{
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public SubscriptionPlan? Plan { get; set; }
    public SubscriptionStatus? Status { get; set; }
    public DateTime? CurrentPeriodEndsAt { get; set; }
}

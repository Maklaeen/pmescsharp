using PmesCSharp.Models;

namespace PmesCSharp.ViewModels.SuperAdmin;

public class CompanySubscriptionListItemViewModel
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = "";
    public string CompanyCode { get; set; } = "";

    public SubscriptionPlan? Plan { get; set; }
    public SubscriptionStatus? Status { get; set; }
    public DateTime? CurrentPeriodEndsAt { get; set; }
}

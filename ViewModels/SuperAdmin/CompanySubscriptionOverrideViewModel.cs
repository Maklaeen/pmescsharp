using System.ComponentModel.DataAnnotations;
using PmesCSharp.Models;

namespace PmesCSharp.ViewModels.SuperAdmin;

public class CompanySubscriptionOverrideViewModel
{
    [Required]
    public int CompanyId { get; set; }

    [Required]
    public SubscriptionPlan Plan { get; set; }

    [Required]
    public SubscriptionStatus Status { get; set; }

    [Required]
    public SubscriptionBillingCycle BillingCycle { get; set; } = SubscriptionBillingCycle.Monthly;

    public DateTime? TrialEndsAt { get; set; }

    public DateTime? CurrentPeriodEndsAt { get; set; }

    [StringLength(256)]
    public string? BillingEmail { get; set; }
}

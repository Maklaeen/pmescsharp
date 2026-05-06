using System.ComponentModel.DataAnnotations;
using PmesCSharp.Models;

namespace PmesCSharp.ViewModels.Subscription;

public class SubscriptionSetupViewModel
{
    [Required]
    public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Standard;

    [EmailAddress]
    public string? BillingEmail { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace PmesCSharp.Models;

public enum SubscriptionPlan
{
    Standard = 1,
    Pro = 2,
}

public enum SubscriptionStatus
{
    Trialing = 1,
    Active = 2,
    PastDue = 3,
    Canceled = 4,
    Expired = 5,
}

public class CompanySubscription
{
    public int Id { get; set; }

    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    [Required]
    public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Standard;

    [Required]
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trialing;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? TrialEndsAt { get; set; }
    public DateTime? CurrentPeriodEndsAt { get; set; }

    [StringLength(256)]
    public string? BillingEmail { get; set; }
}
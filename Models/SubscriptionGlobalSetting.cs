using System.ComponentModel.DataAnnotations;

namespace PmesCSharp.Models;

public class SubscriptionGlobalSetting
{
    public int Id { get; set; }

    [StringLength(200)]
    public string? PayMongoPublicKey { get; set; }

    [StringLength(200)]
    public string? PayMongoSecretKey { get; set; }

    public int TrialDays { get; set; } = 15;

    public int GracePeriodDays { get; set; } = 7;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

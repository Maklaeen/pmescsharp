using System.ComponentModel.DataAnnotations;

namespace PmesCSharp.ViewModels.SuperAdmin;

public class GlobalBillingSettingsViewModel
{
    [StringLength(200)]
    public string? PayMongoPublicKey { get; set; }

    [StringLength(200)]
    public string? PayMongoSecretKey { get; set; }

    [Range(0, 365)]
    public int TrialDays { get; set; } = 15;

    [Range(0, 60)]
    public int GracePeriodDays { get; set; } = 7;
}

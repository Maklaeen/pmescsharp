using System.ComponentModel.DataAnnotations;

namespace PmesCSharp.ViewModels.Account;

public class OtpVerifyViewModel
{
    [Required]
    public string UserId { get; set; } = "";

    [Required]
    [StringLength(6, MinimumLength = 6)]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP must be 6 digits")]
    public string Code { get; set; } = "";

    [Required]
    public string Email { get; set; } = "";

    public bool RememberMe { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace PmesCSharp.ViewModels.Account;

public class RegisterViewModel
{
    [Required]
    [StringLength(255)]
    public string Name { get; set; } = "";

    [Required]
    [StringLength(200)]
    public string CompanyName { get; set; } = "";

    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = "";

    // Google reCAPTCHA response token (v2/v3). When Recaptcha:SecretKey isn't configured, verification is skipped.
    public string? RecaptchaToken { get; set; }
}

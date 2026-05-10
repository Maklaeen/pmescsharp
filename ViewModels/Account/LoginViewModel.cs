using System.ComponentModel.DataAnnotations;

namespace PmesCSharp.ViewModels.Account;

public class LoginViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public bool RememberMe { get; set; }

    // Google reCAPTCHA response token (v2/v3). When Recaptcha:SecretKey isn't configured, verification is skipped.
    public string? RecaptchaToken { get; set; }
}

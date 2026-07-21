using System.ComponentModel.DataAnnotations;

namespace PmesCSharp.ViewModels.Account;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(255)]
    public string Name { get; set; } = "";

    [StringLength(200)]
    public string? CompanyName { get; set; }

    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Please confirm your password.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = "";

    [StringLength(32)]
    public string? InvitationCode { get; set; }

    public string? RecaptchaToken { get; set; }
}

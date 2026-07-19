using System.ComponentModel.DataAnnotations;

namespace PmesCSharp.ViewModels.Account;

public class ForgotPasswordViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";
}

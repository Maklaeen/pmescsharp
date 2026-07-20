using System.ComponentModel.DataAnnotations;

namespace PmesCSharp.ViewModels.Account;

public class OtpLoginViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";
}

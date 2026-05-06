using System.ComponentModel.DataAnnotations;

namespace PmesCSharp.ViewModels.Join;

public class JoinLoginViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";
}

using System.ComponentModel.DataAnnotations;

namespace PmesCSharp.ViewModels.Users;

public class UserFormViewModel
{
    [Required, StringLength(200)]
    public string Name { get; set; } = "";

    [Required, EmailAddress]
    public string Email { get; set; } = "";

    public string? Role { get; set; }

    [DataType(DataType.Password), MinLength(8)]
    public string? Password { get; set; }

    [DataType(DataType.Password), Compare(nameof(Password))]
    public string? PasswordConfirmation { get; set; }
}

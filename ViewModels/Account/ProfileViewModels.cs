using System.ComponentModel.DataAnnotations;

namespace PmesCSharp.ViewModels.Account;

public class UpdateProfileViewModel
{
    [Required, StringLength(200)]
    public string Name { get; set; } = "";

    [Required, EmailAddress]
    public string Email { get; set; } = "";
}

public class ChangePasswordViewModel
{
    [Required, DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = "";

    [Required, DataType(DataType.Password), MinLength(8)]
    public string Password { get; set; } = "";

    [Required, DataType(DataType.Password), Compare(nameof(Password))]
    public string PasswordConfirmation { get; set; } = "";
}

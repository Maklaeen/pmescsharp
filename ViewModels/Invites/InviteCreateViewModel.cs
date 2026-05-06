using System.ComponentModel.DataAnnotations;

namespace PmesCSharp.ViewModels.Invites;

public class InviteCreateViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    public string Role { get; set; } = "operator";

    [Range(1, 30)]
    public int ExpiresInDays { get; set; } = 7;
}

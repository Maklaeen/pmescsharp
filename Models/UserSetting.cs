using System.ComponentModel.DataAnnotations;

namespace PmesCSharp.Models;

public class UserSetting
{
    public int Id { get; set; }

    [Required, StringLength(450)]
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;

    [Required, StringLength(20)]
    public string Theme { get; set; } = "dark"; // dark | light

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

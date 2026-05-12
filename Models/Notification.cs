using System.ComponentModel.DataAnnotations;

namespace PmesCSharp.Models;

public class Notification
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = "";
    public ApplicationUser? User { get; set; }

    [Required, StringLength(200)]
    public string Message { get; set; } = "";

    [StringLength(100)]
    public string? Link { get; set; }

    [StringLength(20)]
    public string Type { get; set; } = "info"; // info, success, warning, error

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

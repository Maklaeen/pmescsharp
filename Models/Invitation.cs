using System.ComponentModel.DataAnnotations;

namespace PmesCSharp.Models;

public class Invitation
{
    public int Id { get; set; }

    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    public string Role { get; set; } = "operator";

    [Required]
    public string Token { get; set; } = "";

    public bool IsAccepted { get; set; } = false;

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? InvitedByUserId { get; set; }
    public ApplicationUser? InvitedByUser { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace PmesCSharp.Models;

public class CompanyInvite
{
    public int Id { get; set; }

    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    [Required]
    [StringLength(256)]
    public string InvitedEmail { get; set; } = "";

    [Required]
    [StringLength(64)]
    public string TokenHash { get; set; } = "";

    [Required]
    [StringLength(32)]
    public string Code { get; set; } = "";

    [StringLength(32)]
    public string Role { get; set; } = "operator";

    public DateTime ExpiresAt { get; set; }
    public int MaxUses { get; set; } = 1;
    public int UsesCount { get; set; } = 0;
    public DateTime? ConsumedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(450)]
    public string? CreatedByUserId { get; set; }
    public ApplicationUser? CreatedByUser { get; set; }

    public DateTime? RevokedAt { get; set; }

    public bool IsActive(DateTime utcNow)
    {
        if (RevokedAt != null) return false;
        if (utcNow >= ExpiresAt) return false;
        if (UsesCount >= MaxUses) return false;
        return true;
    }
}

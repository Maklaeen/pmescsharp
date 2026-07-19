using System.ComponentModel.DataAnnotations;

namespace PmesCSharp.Models;

public class AuditLog
{
    public int Id { get; set; }

    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    [StringLength(450)]
    public string? ActorUserId { get; set; }
    public ApplicationUser? ActorUser { get; set; }

    [Required, StringLength(100)]
    public string Action { get; set; } = "";

    [StringLength(100)]
    public string? EntityType { get; set; }

    [StringLength(100)]
    public string? EntityId { get; set; }

    [StringLength(4000)]
    public string? Details { get; set; }

    [StringLength(45)]
    public string? IpAddress { get; set; }

    [StringLength(255)]
    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

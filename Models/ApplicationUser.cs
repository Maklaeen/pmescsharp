using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace PmesCSharp.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = "";
    public bool IsArchived { get; set; } = false;
    public DateTime? ArchivedAt { get; set; }

    // Admin approval workflow (used for invited employees)
    public bool IsApproved { get; set; } = true;
    public DateTime? ApprovedAt { get; set; }

    [StringLength(32)]
    public string? PendingRole { get; set; }

    public int? CompanyId { get; set; }
    public Company? Company { get; set; }
}

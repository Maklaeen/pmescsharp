using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PmesCSharp.ViewModels.Company;

public class CompanyProfileViewModel
{
    [Required, StringLength(200)]
    public string DisplayName { get; set; } = "";

    [StringLength(255)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? Phone { get; set; }

    [EmailAddress, StringLength(200)]
    public string? Email { get; set; }

    [RegularExpression(@"^(?:https?://|ftp://)?(?:[A-Za-z0-9-]+\.)+[A-Za-z]{2,}(?:\:\d+)?(?:/.*)?$", ErrorMessage = "The Website must be a valid URL like example.com or https://example.com.")]
    [StringLength(200)]
    public string? Website { get; set; }

    [StringLength(200)]
    public string? Industry { get; set; }

    public List<CompanyProfileUserViewModel> Users { get; set; } = new();
}

public class CompanyProfileUserViewModel
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
}

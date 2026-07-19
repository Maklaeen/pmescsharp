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

    [Url, StringLength(200)]
    public string? Website { get; set; }

    [StringLength(200)]
    public string? Industry { get; set; }
}

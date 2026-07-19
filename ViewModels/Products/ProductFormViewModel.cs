using System.ComponentModel.DataAnnotations;

namespace PmesCSharp.ViewModels.Products;

public class ProductFormViewModel
{
    [Required, StringLength(100)]
    public string ProductCode { get; set; } = "";

    [Required, StringLength(200)]
    public string ProductName { get; set; } = "";

    public string? Description { get; set; }

    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; } = 0;

    [StringLength(50)]
    public string Unit { get; set; } = "pcs";

    public string Status { get; set; } = "active";
}

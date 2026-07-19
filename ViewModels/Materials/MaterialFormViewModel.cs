using System.ComponentModel.DataAnnotations;

namespace PmesCSharp.ViewModels.Materials;

public class MaterialFormViewModel
{
    [Required, StringLength(100)]
    public string MaterialCode { get; set; } = "";

    [Required, StringLength(200)]
    public string MaterialName { get; set; } = "";

    public string? Description { get; set; }

    [StringLength(50)]
    public string Unit { get; set; } = "pcs";

    [Range(0, double.MaxValue)]
    public decimal UnitCost { get; set; } = 0;

    [Range(0, double.MaxValue)]
    public decimal StockQuantity { get; set; } = 0;

    [Range(0, double.MaxValue)]
    public decimal ReorderLevel { get; set; } = 0;

    public string Status { get; set; } = "active";
}

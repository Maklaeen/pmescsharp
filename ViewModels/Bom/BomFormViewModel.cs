using System.ComponentModel.DataAnnotations;

namespace PmesCSharp.ViewModels.Bom;

public class BomFormViewModel
{
    [Required]
    public int ProductId { get; set; }

    [Required]
    public int MaterialId { get; set; }

    [Required, Range(0.0001, double.MaxValue)]
    public decimal QuantityRequired { get; set; }

    public string? Unit { get; set; }
}

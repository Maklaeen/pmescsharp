using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PmesCSharp.Models;

public class Material
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string MaterialCode { get; set; } = "";

    [Required, StringLength(200)]
    public string MaterialName { get; set; } = "";

    public string? Description { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal UnitCost { get; set; } = 0;

    [StringLength(50)]
    public string Unit { get; set; } = "pcs";

    [Column(TypeName = "decimal(10,2)")]
    public decimal StockQuantity { get; set; } = 0;

    [Column(TypeName = "decimal(10,2)")]
    public decimal ReorderLevel { get; set; } = 0;

    [StringLength(20)]
    public string Status { get; set; } = "active";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

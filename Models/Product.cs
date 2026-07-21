using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PmesCSharp.Models;

public class Product
{
    public int Id { get; set; }

    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    [Required, StringLength(100)]
    public string ProductCode { get; set; } = "";

    [Required, StringLength(200)]
    public string ProductName { get; set; } = "";

    public string? Description { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal UnitPrice { get; set; } = 0;

    [StringLength(50)]
    public string Unit { get; set; } = "pcs";

    [StringLength(20)]
    public string Status { get; set; } = "active";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsArchived { get; set; } = false;
    public DateTime? ArchivedAt { get; set; }

    public ICollection<BillOfMaterial> BillOfMaterials { get; set; } = [];
    public ICollection<ProductionSchedule> ProductionSchedules { get; set; } = [];
}

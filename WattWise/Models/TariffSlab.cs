using System.ComponentModel.DataAnnotations;

namespace WattWise.Models;

/// <summary>
/// Defines tiered electricity pricing. Each slab covers a unit range
/// and applies a per-unit rate plus any fixed charge for that tier.
/// Example: 0–100 units at ₹3.50/unit, 101–200 at ₹5.00/unit, etc.
/// </summary>
public class TariffSlab
{
    [Key]
    public int TariffSlabId { get; set; }

    [Required(ErrorMessage = "Connection type is required.")]
    [StringLength(50)]
    [Display(Name = "Connection Type")]
    public string ConnectionType { get; set; } = string.Empty;    // Domestic / Commercial / Industrial

    [Required(ErrorMessage = "Slab label is required.")]
    [StringLength(100)]
    [Display(Name = "Slab Label")]
    public string SlabLabel { get; set; } = string.Empty;         // e.g. "0 – 100 units"

    [Range(0, int.MaxValue)]
    [Display(Name = "Min Units")]
    public int MinUnits { get; set; }

    /// <summary>Null means the slab covers all units above MinUnits (open-ended top slab).</summary>
    [Display(Name = "Max Units")]
    public int? MaxUnits { get; set; }

    [Required]
    [Range(0.01, 100.0, ErrorMessage = "Rate per unit must be positive.")]
    [Display(Name = "Rate per Unit (₹)")]
    public decimal RatePerUnit { get; set; }

    [Range(0, 10000)]
    [Display(Name = "Fixed Charge (₹)")]
    public decimal FixedCharge { get; set; } = 0;                 // Monthly fixed charge for this tier

    public bool IsActive { get; set; } = true;

    [Display(Name = "Effective From")]
    [DataType(DataType.Date)]
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
}

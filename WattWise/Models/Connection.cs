using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WattWise.Models;

/// <summary>
/// Represents an electricity service connection belonging to a User.
/// Each connection has its own meter and can accumulate meter readings.
/// </summary>
public class Connection
{
    [Key]
    public int ConnectionId { get; set; }

    [Required(ErrorMessage = "Consumer number is required.")]
    [StringLength(20)]
    [Display(Name = "Consumer Number")]
    public string ConsumerNumber { get; set; } = string.Empty;    // Unique meter/consumer ID

    [Required(ErrorMessage = "Connection type is required.")]
    [StringLength(50)]
    [Display(Name = "Connection Type")]
    public string ConnectionType { get; set; } = string.Empty;    // e.g. Domestic, Commercial, Industrial

    [Required]
    [StringLength(200)]
    public string Address { get; set; } = string.Empty;

    [Range(0.1, 1000.0, ErrorMessage = "Load must be between 0.1 and 1000 kW.")]
    [Display(Name = "Sanctioned Load (kW)")]
    public decimal SanctionedLoad { get; set; }                   // In kilowatts

    public bool IsActive { get; set; } = true;
    public DateTime ConnectedOn { get; set; } = DateTime.UtcNow;

    // ── Solar configuration ───────────────────────────────────────────────────
    /// <summary>True when the property has a solar PV installation.</summary>
    [Display(Name = "Has Solar Installation")]
    public bool HasSolar { get; set; } = false;

    /// <summary>Installed solar capacity in kWp. Optional — for display/reference only.</summary>
    [Range(0.001, 10000, ErrorMessage = "Capacity must be between 0.001 and 10000 kWp.")]
    [Display(Name = "Solar Capacity (kWp)")]
    public decimal? SolarCapacityKw { get; set; }

    /// <summary>True when a battery storage system is present. Affects self-consumption calculation.</summary>
    [Display(Name = "Has Battery Storage")]
    public bool HasBattery { get; set; } = false;

    // Foreign key to User
    [ForeignKey(nameof(User))]
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    // Navigation properties
    public ICollection<MeterReading> MeterReadings { get; set; } = new List<MeterReading>();
}

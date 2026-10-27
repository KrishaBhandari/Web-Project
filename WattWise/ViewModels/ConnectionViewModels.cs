using System.ComponentModel.DataAnnotations;

namespace WattWise.ViewModels;

public class ConnectionCreateViewModel
{
    [Required(ErrorMessage = "Consumer number is required.")]
    [StringLength(20, ErrorMessage = "Consumer number cannot exceed 20 characters.")]
    [Display(Name = "Consumer Number")]
    public string ConsumerNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Connection type is required.")]
    [Display(Name = "Connection Type")]
    public string ConnectionType { get; set; } = "Domestic";

    [Required(ErrorMessage = "Address is required.")]
    [StringLength(200)]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "Sanctioned load is required.")]
    [Range(0.1, 1000.0, ErrorMessage = "Load must be between 0.1 and 1000 kW.")]
    [Display(Name = "Sanctioned Load (kW)")]
    public decimal SanctionedLoad { get; set; }

    // ── Solar fields ──────────────────────────────────────────────────────────
    [Display(Name = "Does this property have a solar installation?")]
    public bool HasSolar { get; set; } = false;

    [Range(0.001, 10000, ErrorMessage = "Capacity must be between 0.001 and 10000 kWp.")]
    [Display(Name = "Solar Installation Capacity (kWp)")]
    public decimal? SolarCapacityKw { get; set; }

    [Display(Name = "Does this property have battery storage?")]
    public bool HasBattery { get; set; } = false;
}

public class ConnectionEditViewModel : ConnectionCreateViewModel
{
    public int ConnectionId { get; set; }
    public bool IsActive { get; set; }
}

public class ConnectionDetailViewModel
{
    public int ConnectionId { get; set; }
    public string ConsumerNumber { get; set; } = string.Empty;
    public string ConnectionType { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public decimal SanctionedLoad { get; set; }
    public bool IsActive { get; set; }
    public DateTime ConnectedOn { get; set; }
    public bool HasSolar { get; set; }
    public decimal? SolarCapacityKw { get; set; }
    public bool HasBattery { get; set; }
    public List<MeterReadingSummaryViewModel> RecentReadings { get; set; } = new();
    public decimal? LatestUnitsConsumed { get; set; }
    public decimal? EstimatedBill { get; set; }
}

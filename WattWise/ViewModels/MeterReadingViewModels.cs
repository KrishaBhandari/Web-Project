using System.ComponentModel.DataAnnotations;

namespace WattWise.ViewModels;

public class MeterReadingCreateViewModel
{
    [Required]
    [Display(Name = "Connection")]
    public int ConnectionId { get; set; }

    [Required(ErrorMessage = "Reading date is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "Reading Date")]
    public DateTime ReadingDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Current grid import reading is required.")]
    [Range(0, 9999999, ErrorMessage = "Invalid reading value.")]
    [Display(Name = "Current Grid Import Reading (kWh)")]
    public decimal CurrentReading { get; set; }

    [StringLength(500)]
    public string? Remarks { get; set; }

    // ── Populated from DB for display ─────────────────────────────────────────
    public decimal? PreviousReading { get; set; }
    public DateTime? PreviousReadingDate { get; set; }
    public string ConnectionDisplay { get; set; } = string.Empty;

    // ── Solar fields (only required when connection.HasSolar=true) ─────────────
    public bool ConnectionHasSolar { get; set; }
    public bool ConnectionHasBattery { get; set; }

    // Solar generation meter (optional — enter if you have a solar gen meter)
    [Range(0, 9999999, ErrorMessage = "Invalid solar generation reading.")]
    [Display(Name = "Current Solar Generation Meter (kWh)")]
    public decimal? CurrSolarGenReading { get; set; }

    [Range(0, 9999999)]
    public decimal? PrevSolarGenReading { get; set; }

    // Grid export meter (optional — enter if your meter records export)
    [Range(0, 9999999, ErrorMessage = "Invalid grid export reading.")]
    [Display(Name = "Current Grid Export Meter (kWh)")]
    public decimal? CurrGridExportReading { get; set; }

    [Range(0, 9999999)]
    public decimal? PrevGridExportReading { get; set; }
}

public class MeterReadingSummaryViewModel
{
    public int ReadingId { get; set; }
    public int ConnectionId { get; set; }
    public string ConsumerNumber { get; set; } = string.Empty;
    public string ConnectionType { get; set; } = string.Empty;
    public bool ConnectionHasSolar { get; set; }
    public DateTime ReadingDate { get; set; }
    public decimal PreviousReading { get; set; }
    public decimal CurrentReading { get; set; }
    public decimal UnitsConsumed => CurrentReading - PreviousReading;
    public decimal BillAmount { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Remarks { get; set; }

    // ── Solar computed values (null = data unavailable) ───────────────────────
    public decimal? SolarGenerationKwh { get; set; }
    public decimal? GridExportKwh { get; set; }
    /// <summary>Null when battery present or generation/export unavailable.</summary>
    public decimal? SolarSelfConsumedKwh { get; set; }
}

public class MeterReadingIndexViewModel
{
    public List<MeterReadingSummaryViewModel> Readings { get; set; } = new();
    public int? FilterConnectionId { get; set; }
    public List<(int Id, string Label)> Connections { get; set; } = new();
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
}

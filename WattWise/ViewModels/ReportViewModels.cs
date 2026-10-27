using System.ComponentModel.DataAnnotations;

namespace WattWise.ViewModels;

/// <summary>Form model for selecting connection + month/year for PDF export.</summary>
public class ReportRequestViewModel
{
    [Required(ErrorMessage = "Please select a connection.")]
    [Display(Name = "Connection")]
    public int ConnectionId { get; set; }

    [Required(ErrorMessage = "Month is required.")]
    [Range(1, 12, ErrorMessage = "Month must be between 1 and 12.")]
    public int Month { get; set; } = DateTime.Today.Month;

    [Required(ErrorMessage = "Year is required.")]
    [Range(2000, 2100, ErrorMessage = "Enter a valid year.")]
    public int Year { get; set; } = DateTime.Today.Year;

    // Populated in controller for display
    public List<(int Id, string Label)> Connections { get; set; } = new();
}

/// <summary>Data assembled from DB and passed to the PDF generation service.</summary>
public class MonthlyReportData
{
    // Connection info
    public string ConsumerNumber { get; set; } = string.Empty;
    public string ConnectionType { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public decimal SanctionedLoad { get; set; }
    public bool HasSolar { get; set; }
    public decimal? SolarCapacityKw { get; set; }
    public bool HasBattery { get; set; }

    // Report period
    public int ReportMonth { get; set; }
    public int ReportYear { get; set; }
    public string MonthLabel => new DateTime(ReportYear, ReportMonth, 1).ToString("MMMM yyyy");

    // Reading data
    public bool HasReadingData { get; set; }
    public DateTime? ReadingDate { get; set; }
    public decimal? PreviousGridReading { get; set; }
    public decimal? CurrentGridReading { get; set; }
    public decimal? GridImportKwh { get; set; }   // CurrentGridReading - PreviousGridReading
    public decimal? BillAmount { get; set; }
    public bool IsPaid { get; set; }
    public string? Remarks { get; set; }

    // Solar data (null = not applicable or data unavailable)
    public decimal? SolarGenerationKwh { get; set; }
    public decimal? GridExportKwh { get; set; }
    /// <summary>Only set when HasBattery=false AND both gen+export are available.</summary>
    public decimal? SolarSelfConsumedKwh { get; set; }

    // Tariff info for context
    public List<TariffSlabInfo> AppliedSlabs { get; set; } = new();

    // Report metadata
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    public string UserName { get; set; } = string.Empty;
}

public class TariffSlabInfo
{
    public string SlabLabel { get; set; } = string.Empty;
    public decimal RatePerUnit { get; set; }
    public decimal FixedCharge { get; set; }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WattWise.Data;
using WattWise.Services;
using WattWise.ViewModels;

namespace WattWise.Controllers;

[Authorize]
public class ReportController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly PdfReportService _pdf;
    private readonly IBillingService _billing;
    private readonly ILogger<ReportController> _logger;

    public ReportController(ApplicationDbContext db, PdfReportService pdf,
        IBillingService billing, ILogger<ReportController> logger)
    {
        _db      = db;
        _pdf     = pdf;
        _billing = billing;
        _logger  = logger;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ── GET /Report ───────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var vm = new ReportRequestViewModel
        {
            Connections = await GetConnectionList(),
            Month       = DateTime.Today.Month,
            Year        = DateTime.Today.Year
        };
        return View(vm);
    }

    // ── GET /Report/Download?connectionId=1&month=10&year=2026 ───────────────
    [HttpGet]
    public async Task<IActionResult> Download(int connectionId, int month, int year)
    {
        // ── Validate inputs ───────────────────────────────────────────────────
        if (month < 1 || month > 12 || year < 2000 || year > 2100)
            return BadRequest("Invalid month or year.");

        // ── Authorisation: connection must belong to the current user ─────────
        var connection = await _db.Connections
            .Where(c => c.ConnectionId == connectionId && c.UserId == CurrentUserId)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (connection == null)
            return NotFound("Connection not found or access denied.");

        // ── Find the reading for the requested month ──────────────────────────
        var periodStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd   = periodStart.AddMonths(1);

        var reading = await _db.MeterReadings
            .Where(m => m.ConnectionId == connectionId
                     && m.ReadingDate >= periodStart
                     && m.ReadingDate < periodEnd)
            .OrderByDescending(m => m.ReadingDate)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        // ── Tariff slabs ──────────────────────────────────────────────────────
        var slabs = await _db.TariffSlabs
            .Where(t => t.ConnectionType == connection.ConnectionType && t.IsActive)
            .OrderBy(t => t.MinUnits)
            .AsNoTracking()
            .ToListAsync();

        var userName = User.FindFirstValue(ClaimTypes.Name) ?? "Unknown";

        // ── Assemble report data ──────────────────────────────────────────────
        decimal? solarSelf = null;
        if (reading != null && connection.HasSolar && !connection.HasBattery
            && reading.SolarGenerationKwh.HasValue && reading.GridExportKwh.HasValue)
        {
            solarSelf = reading.SolarGenerationKwh.Value - reading.GridExportKwh.Value;
        }

        var data = new MonthlyReportData
        {
            // Connection
            ConsumerNumber  = connection.ConsumerNumber,
            ConnectionType  = connection.ConnectionType,
            Address         = connection.Address,
            SanctionedLoad  = connection.SanctionedLoad,
            HasSolar        = connection.HasSolar,
            SolarCapacityKw = connection.SolarCapacityKw,
            HasBattery      = connection.HasBattery,

            // Period
            ReportMonth = month,
            ReportYear  = year,

            // Reading
            HasReadingData       = reading != null,
            ReadingDate          = reading?.ReadingDate.ToLocalTime(),
            PreviousGridReading  = reading?.PreviousReading,
            CurrentGridReading   = reading?.CurrentReading,
            GridImportKwh        = reading != null
                                    ? reading.CurrentReading - reading.PreviousReading
                                    : null,
            BillAmount           = reading?.BillAmount,
            IsPaid               = reading?.IsPaid ?? false,
            Remarks              = reading?.Remarks,

            // Solar
            SolarGenerationKwh   = connection.HasSolar ? reading?.SolarGenerationKwh : null,
            GridExportKwh        = connection.HasSolar ? reading?.GridExportKwh : null,
            SolarSelfConsumedKwh = solarSelf,

            // Tariff
            AppliedSlabs = slabs.Select(s => new TariffSlabInfo
            {
                SlabLabel   = s.SlabLabel,
                RatePerUnit = s.RatePerUnit,
                FixedCharge = s.FixedCharge
            }).ToList(),

            GeneratedAt = DateTime.Now,
            UserName    = userName
        };

        // ── Generate PDF ──────────────────────────────────────────────────────
        try
        {
            byte[] pdfBytes = _pdf.Generate(data);
            string filename = $"WattWise_{connection.ConsumerNumber}_{year}-{month:D2}.pdf";

            _logger.LogInformation("PDF report generated: {File} for user {UserId}", filename, CurrentUserId);
            return File(pdfBytes, "application/pdf", filename);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF generation failed for connection {Id}", connectionId);
            TempData["Error"] = "PDF generation failed. Please try again.";
            return RedirectToAction(nameof(Index));
        }
    }

    private async Task<List<(int Id, string Label)>> GetConnectionList()
    {
        return await _db.Connections
            .Where(c => c.UserId == CurrentUserId)
            .OrderBy(c => c.ConsumerNumber)
            .Select(c => new { c.ConnectionId,
                               Label = c.ConsumerNumber + " — " + c.Address })
            .AsNoTracking()
            .ToListAsync()
            .ContinueWith(t => t.Result.Select(x => (x.ConnectionId, x.Label)).ToList());
    }
}

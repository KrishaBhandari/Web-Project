using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WattWise.Data;
using WattWise.Models;
using WattWise.Services;
using WattWise.ViewModels;

namespace WattWise.Controllers;

[Authorize]
public class MeterReadingsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IBillingService _billing;
    private readonly ILogger<MeterReadingsController> _logger;

    public MeterReadingsController(ApplicationDbContext db, IBillingService billing,
        ILogger<MeterReadingsController> logger)
    {
        _db      = db;
        _billing = billing;
        _logger  = logger;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ── GET /MeterReadings ────────────────────────────────────────────────────
    public async Task<IActionResult> Index(int? connectionId, int page = 1)
    {
        const int pageSize = 10;
        var userId = CurrentUserId;

        var query = _db.MeterReadings
            .Where(m => m.Connection.UserId == userId)
            .Include(m => m.Connection)
            .AsQueryable();

        if (connectionId.HasValue)
            query = query.Where(m => m.ConnectionId == connectionId.Value);

        var total   = await query.CountAsync();
        var readings = await query
            .OrderByDescending(m => m.ReadingDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .Select(m => new MeterReadingSummaryViewModel
            {
                ReadingId            = m.ReadingId,
                ConnectionId         = m.ConnectionId,
                ConsumerNumber       = m.Connection.ConsumerNumber,
                ConnectionType       = m.Connection.ConnectionType,
                ConnectionHasSolar   = m.Connection.HasSolar,
                ReadingDate          = m.ReadingDate,
                PreviousReading      = m.PreviousReading,
                CurrentReading       = m.CurrentReading,
                BillAmount           = m.BillAmount,
                IsPaid               = m.IsPaid,
                DueDate              = m.DueDate,
                Remarks              = m.Remarks,
                SolarGenerationKwh   = m.Connection.HasSolar ? m.SolarGenerationKwh : null,
                GridExportKwh        = m.Connection.HasSolar ? m.GridExportKwh : null,
                SolarSelfConsumedKwh = (m.Connection.HasSolar && !m.Connection.HasBattery)
                                         ? m.SolarSelfConsumedKwh : null
            }).ToListAsync();

        var connections = await _db.Connections
            .Where(c => c.UserId == userId)
            .Select(c => new { c.ConnectionId, Label = c.ConsumerNumber + " – " + c.Address })
            .ToListAsync();

        var vm = new MeterReadingIndexViewModel
        {
            Readings           = readings,
            FilterConnectionId = connectionId,
            Connections        = connections.Select(c => (c.ConnectionId, c.Label)).ToList(),
            Page               = page,
            TotalPages         = (int)Math.Ceiling(total / (double)pageSize)
        };
        return View(vm);
    }

    // ── GET /MeterReadings/Create?connectionId=5 ──────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Create(int? connectionId)
    {
        var userId = CurrentUserId;
        var connections = await _db.Connections
            .Where(c => c.UserId == userId && c.IsActive)
            .OrderBy(c => c.ConsumerNumber)
            .ToListAsync();

        if (!connections.Any())
        {
            TempData["Warning"] = "Please add an electricity connection before entering a meter reading.";
            return RedirectToAction("Create", "Connections");
        }

        ViewBag.Connections = new SelectList(connections, "ConnectionId", "ConsumerNumber", connectionId);

        var selectedConnId = connectionId ?? connections.First().ConnectionId;
        var selectedConn   = connections.First(c => c.ConnectionId == selectedConnId);

        var vm = new MeterReadingCreateViewModel
        {
            ConnectionId         = selectedConnId,
            ReadingDate          = DateTime.Today,
            ConnectionHasSolar   = selectedConn.HasSolar,
            ConnectionHasBattery = selectedConn.HasBattery
        };

        await PopulatePreviousReading(vm, userId);
        return View(vm);
    }

    // ── POST /MeterReadings/Create ────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MeterReadingCreateViewModel model)
    {
        var userId = CurrentUserId;
        var connections = await _db.Connections
            .Where(c => c.UserId == userId && c.IsActive)
            .OrderBy(c => c.ConsumerNumber)
            .ToListAsync();
        ViewBag.Connections = new SelectList(connections, "ConnectionId", "ConsumerNumber", model.ConnectionId);

        var connection = connections.FirstOrDefault(c => c.ConnectionId == model.ConnectionId);
        if (connection == null)
        {
            ModelState.AddModelError("ConnectionId", "Invalid connection selected.");
            return View(model);
        }

        model.ConnectionHasSolar   = connection.HasSolar;
        model.ConnectionHasBattery = connection.HasBattery;

        // Clear solar validation when connection is non-solar
        if (!connection.HasSolar)
        {
            ModelState.Remove("CurrSolarGenReading");
            ModelState.Remove("PrevSolarGenReading");
            ModelState.Remove("CurrGridExportReading");
            ModelState.Remove("PrevGridExportReading");
            model.CurrSolarGenReading   = null;
            model.PrevSolarGenReading   = null;
            model.CurrGridExportReading = null;
            model.PrevGridExportReading = null;
        }

        // Get previous grid reading
        var previous = await _db.MeterReadings
            .Where(m => m.ConnectionId == model.ConnectionId)
            .OrderByDescending(m => m.ReadingDate)
            .FirstOrDefaultAsync();

        decimal prevGridValue  = previous?.CurrentReading ?? 0;
        DateTime? prevDate     = previous?.ReadingDate;

        // ── Grid import validation ────────────────────────────────────────────
        if (model.CurrentReading < prevGridValue)
        {
            ModelState.AddModelError("CurrentReading",
                $"Grid reading ({model.CurrentReading}) cannot be less than the previous reading ({prevGridValue}).");
        }
        if (prevDate.HasValue && model.ReadingDate.Date <= prevDate.Value.Date)
        {
            ModelState.AddModelError("ReadingDate",
                $"Reading date must be after the previous reading date ({prevDate.Value:dd MMM yyyy}).");
        }
        if (model.ReadingDate.Date > DateTime.Today)
        {
            ModelState.AddModelError("ReadingDate", "Reading date cannot be in the future.");
        }

        // ── Solar validation (only when connection.HasSolar=true) ─────────────
        if (connection.HasSolar)
        {
            // Solar gen meter: current must be >= previous
            if (model.CurrSolarGenReading.HasValue && model.PrevSolarGenReading.HasValue
                && model.CurrSolarGenReading < model.PrevSolarGenReading)
            {
                ModelState.AddModelError("CurrSolarGenReading",
                    "Solar generation reading cannot be less than the previous solar reading.");
            }
            // Export meter: current must be >= previous
            if (model.CurrGridExportReading.HasValue && model.PrevGridExportReading.HasValue
                && model.CurrGridExportReading < model.PrevGridExportReading)
            {
                ModelState.AddModelError("CurrGridExportReading",
                    "Grid export reading cannot be less than the previous export reading.");
            }
            // Without battery: export cannot exceed generation in this period
            if (!connection.HasBattery
                && model.CurrSolarGenReading.HasValue && model.PrevSolarGenReading.HasValue
                && model.CurrGridExportReading.HasValue && model.PrevGridExportReading.HasValue)
            {
                var periodGen    = model.CurrSolarGenReading.Value - model.PrevSolarGenReading.Value;
                var periodExport = model.CurrGridExportReading.Value - model.PrevGridExportReading.Value;
                if (periodExport > periodGen)
                {
                    ModelState.AddModelError("CurrGridExportReading",
                        $"Grid export this period ({periodExport:F2} kWh) cannot exceed solar generation " +
                        $"({periodGen:F2} kWh) when no battery is installed.");
                }
            }
        }

        if (!ModelState.IsValid)
        {
            model.PreviousReading     = prevGridValue;
            model.PreviousReadingDate = prevDate;
            return View(model);
        }

        decimal unitsConsumed = model.CurrentReading - prevGridValue;
        decimal bill          = _billing.CalculateBill(unitsConsumed, connection.ConnectionType);

        var reading = new MeterReading
        {
            ConnectionId          = model.ConnectionId,
            ReadingDate           = model.ReadingDate.ToUniversalTime(),
            PreviousReading       = prevGridValue,
            CurrentReading        = model.CurrentReading,
            BillAmount            = bill,
            IsPaid                = false,
            DueDate               = model.ReadingDate.AddDays(30).ToUniversalTime(),
            Remarks               = model.Remarks?.Trim(),
            CreatedAt             = DateTime.UtcNow,
            // Solar fields (null for non-solar)
            PrevSolarGenReading   = connection.HasSolar ? model.PrevSolarGenReading : null,
            CurrSolarGenReading   = connection.HasSolar ? model.CurrSolarGenReading : null,
            PrevGridExportReading = connection.HasSolar ? model.PrevGridExportReading : null,
            CurrGridExportReading = connection.HasSolar ? model.CurrGridExportReading : null
        };

        _db.MeterReadings.Add(reading);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Meter reading saved: Connection={Id}, GridUnits={Units}, Bill={Bill}",
            model.ConnectionId, unitsConsumed, bill);

        var solarMsg = connection.HasSolar && reading.SolarGenerationKwh.HasValue
            ? $" | Solar generated: {reading.SolarGenerationKwh.Value:F2} kWh" : "";
        TempData["Success"] = $"Reading saved! Grid: {unitsConsumed:F2} kWh consumed | Estimated bill: ₹{bill:F2}{solarMsg}";
        return RedirectToAction(nameof(Index));
    }

    // ── GET /MeterReadings/Details/5 ─────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var userId  = CurrentUserId;
        var reading = await _db.MeterReadings
            .Where(m => m.ReadingId == id && m.Connection.UserId == userId)
            .Include(m => m.Connection)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (reading == null) return NotFound();

        var vm = new MeterReadingSummaryViewModel
        {
            ReadingId            = reading.ReadingId,
            ConnectionId         = reading.ConnectionId,
            ConsumerNumber       = reading.Connection.ConsumerNumber,
            ConnectionType       = reading.Connection.ConnectionType,
            ConnectionHasSolar   = reading.Connection.HasSolar,
            ReadingDate          = reading.ReadingDate,
            PreviousReading      = reading.PreviousReading,
            CurrentReading       = reading.CurrentReading,
            BillAmount           = reading.BillAmount,
            IsPaid               = reading.IsPaid,
            DueDate              = reading.DueDate,
            Remarks              = reading.Remarks,
            SolarGenerationKwh   = reading.Connection.HasSolar ? reading.SolarGenerationKwh : null,
            GridExportKwh        = reading.Connection.HasSolar ? reading.GridExportKwh : null,
            SolarSelfConsumedKwh = (reading.Connection.HasSolar && !reading.Connection.HasBattery)
                                     ? reading.SolarSelfConsumedKwh : null
        };
        return View(vm);
    }

    // ── POST /MeterReadings/MarkPaid ──────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkPaid(int id)
    {
        var reading = await _db.MeterReadings
            .Where(m => m.ReadingId == id && m.Connection.UserId == CurrentUserId)
            .Include(m => m.Connection)
            .FirstOrDefaultAsync();
        if (reading == null) return NotFound();
        reading.IsPaid = true;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Bill marked as paid.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ── POST /MeterReadings/Delete ────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var reading = await _db.MeterReadings
            .Where(m => m.ReadingId == id && m.Connection.UserId == CurrentUserId)
            .FirstOrDefaultAsync();
        if (reading == null) return NotFound();

        var isLatest = !await _db.MeterReadings
            .AnyAsync(m => m.ConnectionId == reading.ConnectionId && m.ReadingDate > reading.ReadingDate);
        if (!isLatest)
        {
            TempData["Error"] = "Only the most recent reading can be deleted.";
            return RedirectToAction(nameof(Index));
        }

        _db.MeterReadings.Remove(reading);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Meter reading deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ── Helper ────────────────────────────────────────────────────────────────
    private async Task PopulatePreviousReading(MeterReadingCreateViewModel vm, int userId)
    {
        var prev = await _db.MeterReadings
            .Where(m => m.ConnectionId == vm.ConnectionId && m.Connection.UserId == userId)
            .OrderByDescending(m => m.ReadingDate)
            .FirstOrDefaultAsync();

        if (prev != null)
        {
            vm.PreviousReading        = prev.CurrentReading;
            vm.PreviousReadingDate    = prev.ReadingDate;
            // Pre-fill previous solar readings for continuity
            vm.PrevSolarGenReading    = prev.CurrSolarGenReading;
            vm.PrevGridExportReading  = prev.CurrGridExportReading;
        }

        var conn = await _db.Connections
            .Where(c => c.ConnectionId == vm.ConnectionId)
            .Select(c => c.ConsumerNumber + " – " + c.Address)
            .FirstOrDefaultAsync();
        vm.ConnectionDisplay = conn ?? string.Empty;
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WattWise.Data;
using WattWise.Models;
using WattWise.ViewModels;

namespace WattWise.Controllers;

[Authorize]
public class ConnectionsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ConnectionsController> _logger;

    public ConnectionsController(ApplicationDbContext db, ILogger<ConnectionsController> logger)
    {
        _db    = db;
        _logger = logger;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ── GET /Connections ──────────────────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        var list = await _db.Connections
            .Where(c => c.UserId == CurrentUserId)
            .OrderByDescending(c => c.ConnectedOn)
            .AsNoTracking()
            .ToListAsync();
        return View(list);
    }

    // ── GET /Connections/Create ───────────────────────────────────────────────
    [HttpGet]
    public IActionResult Create() => View(new ConnectionCreateViewModel());

    // ── POST /Connections/Create ──────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ConnectionCreateViewModel model)
    {
        // Clear solar validation errors when not a solar connection
        if (!model.HasSolar)
        {
            ModelState.Remove("SolarCapacityKw");
            model.SolarCapacityKw = null;
            model.HasBattery = false;
        }

        if (!ModelState.IsValid) return View(model);

        var duplicate = await _db.Connections
            .AnyAsync(c => c.ConsumerNumber == model.ConsumerNumber);
        if (duplicate)
        {
            ModelState.AddModelError("ConsumerNumber", "This consumer number is already registered.");
            return View(model);
        }

        var conn = new Connection
        {
            UserId         = CurrentUserId,
            ConsumerNumber = model.ConsumerNumber.Trim(),
            ConnectionType = model.ConnectionType,
            Address        = model.Address.Trim(),
            SanctionedLoad = model.SanctionedLoad,
            IsActive       = true,
            ConnectedOn    = DateTime.UtcNow,
            HasSolar       = model.HasSolar,
            SolarCapacityKw = model.HasSolar ? model.SolarCapacityKw : null,
            HasBattery     = model.HasSolar && model.HasBattery
        };

        _db.Connections.Add(conn);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Connection {No} created (solar={Solar}) for user {UserId}",
            conn.ConsumerNumber, conn.HasSolar, CurrentUserId);

        TempData["Success"] = $"Connection {conn.ConsumerNumber} added successfully.";
        return RedirectToAction(nameof(Index));
    }

    // ── GET /Connections/Details/5 ────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var conn = await _db.Connections
            .Where(c => c.ConnectionId == id && c.UserId == CurrentUserId)
            .Include(c => c.MeterReadings.OrderByDescending(m => m.ReadingDate).Take(10))
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (conn == null) return NotFound();

        var readings = conn.MeterReadings.Select(m => new MeterReadingSummaryViewModel
        {
            ReadingId            = m.ReadingId,
            ConnectionId         = m.ConnectionId,
            ConsumerNumber       = conn.ConsumerNumber,
            ConnectionType       = conn.ConnectionType,
            ConnectionHasSolar   = conn.HasSolar,
            ReadingDate          = m.ReadingDate,
            PreviousReading      = m.PreviousReading,
            CurrentReading       = m.CurrentReading,
            BillAmount           = m.BillAmount,
            IsPaid               = m.IsPaid,
            DueDate              = m.DueDate,
            Remarks              = m.Remarks,
            SolarGenerationKwh   = conn.HasSolar ? m.SolarGenerationKwh : null,
            GridExportKwh        = conn.HasSolar ? m.GridExportKwh : null,
            SolarSelfConsumedKwh = conn.HasSolar && !conn.HasBattery ? m.SolarSelfConsumedKwh : null
        }).ToList();

        var latestReading = readings.FirstOrDefault();
        var vm = new ConnectionDetailViewModel
        {
            ConnectionId        = conn.ConnectionId,
            ConsumerNumber      = conn.ConsumerNumber,
            ConnectionType      = conn.ConnectionType,
            Address             = conn.Address,
            SanctionedLoad      = conn.SanctionedLoad,
            IsActive            = conn.IsActive,
            ConnectedOn         = conn.ConnectedOn,
            HasSolar            = conn.HasSolar,
            SolarCapacityKw     = conn.SolarCapacityKw,
            HasBattery          = conn.HasBattery,
            RecentReadings      = readings,
            LatestUnitsConsumed = latestReading?.UnitsConsumed,
            EstimatedBill       = latestReading?.BillAmount
        };
        return View(vm);
    }

    // ── GET /Connections/Edit/5 ───────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var conn = await _db.Connections
            .Where(c => c.ConnectionId == id && c.UserId == CurrentUserId)
            .FirstOrDefaultAsync();
        if (conn == null) return NotFound();

        var vm = new ConnectionEditViewModel
        {
            ConnectionId    = conn.ConnectionId,
            ConsumerNumber  = conn.ConsumerNumber,
            ConnectionType  = conn.ConnectionType,
            Address         = conn.Address,
            SanctionedLoad  = conn.SanctionedLoad,
            IsActive        = conn.IsActive,
            HasSolar        = conn.HasSolar,
            SolarCapacityKw = conn.SolarCapacityKw,
            HasBattery      = conn.HasBattery
        };
        return View(vm);
    }

    // ── POST /Connections/Edit/5 ──────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ConnectionEditViewModel model)
    {
        if (id != model.ConnectionId) return BadRequest();

        if (!model.HasSolar)
        {
            ModelState.Remove("SolarCapacityKw");
            model.SolarCapacityKw = null;
            model.HasBattery = false;
        }

        if (!ModelState.IsValid) return View(model);

        var conn = await _db.Connections
            .Where(c => c.ConnectionId == id && c.UserId == CurrentUserId)
            .FirstOrDefaultAsync();
        if (conn == null) return NotFound();

        var duplicate = await _db.Connections
            .AnyAsync(c => c.ConsumerNumber == model.ConsumerNumber && c.ConnectionId != id);
        if (duplicate)
        {
            ModelState.AddModelError("ConsumerNumber", "This consumer number is already in use.");
            return View(model);
        }

        conn.ConsumerNumber  = model.ConsumerNumber.Trim();
        conn.ConnectionType  = model.ConnectionType;
        conn.Address         = model.Address.Trim();
        conn.SanctionedLoad  = model.SanctionedLoad;
        conn.IsActive        = model.IsActive;
        conn.HasSolar        = model.HasSolar;
        conn.SolarCapacityKw = model.HasSolar ? model.SolarCapacityKw : null;
        conn.HasBattery      = model.HasSolar && model.HasBattery;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Connection updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ── GET /Connections/Delete/5 ─────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var conn = await _db.Connections
            .Where(c => c.ConnectionId == id && c.UserId == CurrentUserId)
            .AsNoTracking().FirstOrDefaultAsync();
        if (conn == null) return NotFound();
        return View(conn);
    }

    // ── POST /Connections/Delete/5 ────────────────────────────────────────────
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var conn = await _db.Connections
            .Where(c => c.ConnectionId == id && c.UserId == CurrentUserId)
            .FirstOrDefaultAsync();
        if (conn == null) return NotFound();
        _db.Connections.Remove(conn);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Connection {conn.ConsumerNumber} deleted.";
        return RedirectToAction(nameof(Index));
    }
}

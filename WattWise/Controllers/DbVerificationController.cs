using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WattWise.Data;
using WattWise.Models;

namespace WattWise.Controllers;

/// <summary>
/// DEV-ONLY controller to verify real database read/write operations.
/// These endpoints prove actual MySQL connectivity — not mock data.
///
/// IMPORTANT: Remove this controller (or restrict it behind authorization)
/// before any production deployment.
/// </summary>
[Route("dev/db")]
public class DbVerificationController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<DbVerificationController> _logger;

    public DbVerificationController(ApplicationDbContext db, ILogger<DbVerificationController> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ── GET /dev/db/test ──────────────────────────────────────────────────────
    // Inserts a test TariffSlab row, reads it back, then deletes it.
    // Returns a JSON report. No persistent data is left in the database.
    [HttpGet("test")]
    public async Task<IActionResult> TestReadWrite()
    {
        var report = new List<string>();
        TariffSlab? inserted = null;

        try
        {
            // ── WRITE ─────────────────────────────────────────────────────────
            inserted = new TariffSlab
            {
                ConnectionType = "Test",
                SlabLabel      = "DB-VERIFY-TEST",
                MinUnits       = 0,
                MaxUnits       = 1,
                RatePerUnit    = 0.01m,
                FixedCharge    = 0.00m,
                IsActive       = false,
                EffectiveFrom  = DateTime.UtcNow
            };

            _db.TariffSlabs.Add(inserted);
            await _db.SaveChangesAsync();
            report.Add($"✅ WRITE: Inserted TariffSlab with Id={inserted.TariffSlabId}");
            _logger.LogInformation("DB verification: inserted TariffSlabId={Id}", inserted.TariffSlabId);

            // ── READ ──────────────────────────────────────────────────────────
            var readBack = await _db.TariffSlabs
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TariffSlabId == inserted.TariffSlabId);

            if (readBack is null)
            {
                report.Add("❌ READ: Row not found after insert.");
                return StatusCode(500, new { success = false, steps = report });
            }
            report.Add($"✅ READ:  Found row — Label='{readBack.SlabLabel}', Rate={readBack.RatePerUnit}");

            // ── CLEANUP ───────────────────────────────────────────────────────
            _db.TariffSlabs.Remove(inserted);
            await _db.SaveChangesAsync();
            report.Add($"✅ DELETE: Removed test row Id={inserted.TariffSlabId}");

            // ── COUNT ─────────────────────────────────────────────────────────
            var totalSlabs = await _db.TariffSlabs.CountAsync();
            report.Add($"✅ COUNT:  TariffSlabs table now has {totalSlabs} row(s)");

            return Ok(new
            {
                success   = true,
                message   = "All database operations succeeded. MySQL is fully connected.",
                steps     = report,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DB verification failed");

            // Clean up if insert succeeded but later step failed
            if (inserted?.TariffSlabId > 0)
            {
                try
                {
                    _db.TariffSlabs.Remove(inserted);
                    await _db.SaveChangesAsync();
                    report.Add("🧹 CLEANUP: Partial cleanup of test row completed.");
                }
                catch { /* swallow cleanup error */ }
            }

            report.Add($"❌ ERROR: {ex.GetType().Name} — {ex.Message}");
            return StatusCode(503, new
            {
                success   = false,
                message   = "Database operation failed. Check the server log for details.",
                steps     = report,
                timestamp = DateTime.UtcNow
            });
        }
    }

    // ── GET /dev/db/tables ────────────────────────────────────────────────────
    // Lists all tables currently visible in wattwise_db.
    [HttpGet("tables")]
    public async Task<IActionResult> ListTables()
    {
        try
        {
            var tables = new List<string>();
            var conn   = _db.Database.GetDbConnection();
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SHOW TABLES;";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                tables.Add(reader.GetString(0));

            await conn.CloseAsync();

            return Ok(new
            {
                database  = "wattwise_db",
                tables    = tables,
                count     = tables.Count,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list tables");
            return StatusCode(503, new
            {
                success = false,
                message = "Could not query table list. Ensure migrations have been applied.",
                error   = ex.GetType().Name
            });
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WattWise.Data;
using WattWise.Services;
using WattWise.ViewModels;

namespace WattWise.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IBillingService _billing;

    public DashboardController(ApplicationDbContext db, IBillingService billing)
    {
        _db     = db;
        _billing = billing;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ── GET /Dashboard ────────────────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        var userId = CurrentUserId;

        var connections = await _db.Connections
            .Where(c => c.UserId == userId)
            .Include(c => c.MeterReadings.OrderByDescending(m => m.ReadingDate).Take(2))
            .AsNoTracking()
            .ToListAsync();

        var now       = DateTime.Now;
        var thisMonth = new DateTime(now.Year, now.Month, 1);
        var lastMonth = thisMonth.AddMonths(-1);

        // ── Monthly consumption (last 6 months) ──────────────────────────
        var sixMonthsAgo = thisMonth.AddMonths(-6);
        var allReadings = await _db.MeterReadings
            .Where(m => m.Connection.UserId == userId && m.ReadingDate >= sixMonthsAgo)
            .Include(m => m.Connection)
            .OrderBy(m => m.ReadingDate)
            .AsNoTracking()
            .ToListAsync();

        var monthlyData = allReadings
            .GroupBy(m => new { m.ReadingDate.Year, m.ReadingDate.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new MonthlyConsumptionPoint
            {
                Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                Units = g.Sum(m => m.CurrentReading - m.PreviousReading),
                Bill  = g.Sum(m => m.BillAmount)
            }).ToList();

        // ── This month / last month ───────────────────────────────────────
        var thisMonthReadings = allReadings
            .Where(m => m.ReadingDate.Year == now.Year && m.ReadingDate.Month == now.Month).ToList();
        var lastMonthReadings = allReadings
            .Where(m => m.ReadingDate.Year == lastMonth.Year && m.ReadingDate.Month == lastMonth.Month).ToList();

        decimal unitsThisMonth = thisMonthReadings.Sum(m => m.CurrentReading - m.PreviousReading);
        decimal unitsLastMonth = lastMonthReadings.Sum(m => m.CurrentReading - m.PreviousReading);

        // ── Connection summaries ─────────────────────────────────────────
        var summaries = connections.Select(c =>
        {
            var latest = c.MeterReadings.FirstOrDefault();
            var prev   = c.MeterReadings.Skip(1).FirstOrDefault();
            decimal? units = latest != null ? latest.CurrentReading - (prev?.CurrentReading ?? latest.PreviousReading) : null;
            return new ConnectionSummary
            {
                ConnectionId     = c.ConnectionId,
                ConsumerNumber   = c.ConsumerNumber,
                ConnectionType   = c.ConnectionType,
                Address          = c.Address,
                LatestUnits      = latest != null ? latest.CurrentReading - latest.PreviousReading : null,
                LatestBill       = latest?.BillAmount,
                LatestReadingDate = latest?.ReadingDate,
                IsActive         = c.IsActive
            };
        }).ToList();

        var vm = new DashboardViewModel
        {
            UserName               = User.FindFirstValue(ClaimTypes.Name) ?? "User",
            TotalConnections       = connections.Count,
            ActiveConnections      = connections.Count(c => c.IsActive),
            TotalUnitsThisMonth    = unitsThisMonth,
            EstimatedBillThisMonth = thisMonthReadings.Sum(m => m.BillAmount),
            TotalUnitsLastMonth    = unitsLastMonth,
            EstimatedBillLastMonth = lastMonthReadings.Sum(m => m.BillAmount),
            HighConsumptionWarning = unitsThisMonth > 300,
            MonthlyConsumption     = monthlyData,
            ConnectionSummaries    = summaries
        };

        return View(vm);
    }
}

using WattWise.Data;

namespace WattWise.Services;

/// <summary>
/// Concrete implementation of IBillingService.
/// Reads active TariffSlabs from the database and applies them to compute the bill.
/// 
/// Billing logic (tiered / slab model):
///   - Only the units that FALL WITHIN each slab are charged at that slab's rate.
///   - The highest applicable slab's FixedCharge is added once.
/// 
/// Example — Domestic, 250 units:
///   Slab 1: 0–100  → 100 units × ₹3.50 = ₹350
///   Slab 2: 101–200 → 100 units × ₹5.00 = ₹500
///   Slab 3: 201–500 → 50 units  × ₹7.00 = ₹350  + ₹150 fixed
///   Total = ₹1,350
/// </summary>
public class BillingService : IBillingService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<BillingService> _logger;

    public BillingService(ApplicationDbContext db, ILogger<BillingService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public decimal CalculateBill(decimal unitsConsumed, string connectionType)
    {
        if (unitsConsumed <= 0)
            return 0m;

        // Fetch active slabs for this connection type, ordered by MinUnits ascending
        var slabs = _db.TariffSlabs
            .Where(t => t.ConnectionType == connectionType && t.IsActive)
            .OrderBy(t => t.MinUnits)
            .ToList();

        if (!slabs.Any())
        {
            _logger.LogWarning("No active tariff slabs found for connection type '{Type}'.", connectionType);
            return 0m;
        }

        decimal totalBill = 0m;
        decimal remainingUnits = unitsConsumed;
        decimal fixedCharge = 0m;

        foreach (var slab in slabs)
        {
            if (remainingUnits <= 0) break;

            // Units that fall in this slab
            decimal slabCapacity = slab.MaxUnits.HasValue
                ? (decimal)(slab.MaxUnits.Value - slab.MinUnits + 1)
                : remainingUnits;   // Open-ended top slab gets all remaining units

            decimal unitsInSlab = Math.Min(remainingUnits, slabCapacity);

            totalBill      += unitsInSlab * slab.RatePerUnit;
            fixedCharge     = slab.FixedCharge;   // Keep updating; last matched slab wins
            remainingUnits -= unitsInSlab;
        }

        totalBill += fixedCharge;

        _logger.LogDebug("Bill calculated: {Units} units, {Type} → ₹{Bill}", unitsConsumed, connectionType, totalBill);
        return Math.Round(totalBill, 2);
    }
}

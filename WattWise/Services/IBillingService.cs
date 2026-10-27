namespace WattWise.Services;

/// <summary>
/// Calculates electricity bill amounts from meter readings using the active tariff slabs.
/// The calculation logic is encapsulated here so it can be unit-tested independently.
/// </summary>
public interface IBillingService
{
    /// <summary>
    /// Computes the electricity bill for the given number of units consumed
    /// based on the tiered slab system.
    /// </summary>
    /// <param name="unitsConsumed">Net units = CurrentReading - PreviousReading</param>
    /// <param name="connectionType">e.g. "Domestic", "Commercial", "Industrial"</param>
    /// <returns>Total bill amount in INR (₹)</returns>
    decimal CalculateBill(decimal unitsConsumed, string connectionType);
}

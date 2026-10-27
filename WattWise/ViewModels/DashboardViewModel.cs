namespace WattWise.ViewModels;

public class DashboardViewModel
{
    public int TotalConnections { get; set; }
    public int ActiveConnections { get; set; }
    public decimal TotalUnitsThisMonth { get; set; }
    public decimal EstimatedBillThisMonth { get; set; }
    public decimal TotalUnitsLastMonth { get; set; }
    public decimal EstimatedBillLastMonth { get; set; }
    public bool HighConsumptionWarning { get; set; }
    public decimal HighConsumptionThreshold { get; set; } = 300m; // Configurable kWh/month warning
    public List<MonthlyConsumptionPoint> MonthlyConsumption { get; set; } = new();
    public List<ConnectionSummary> ConnectionSummaries { get; set; } = new();
    public string UserName { get; set; } = string.Empty;
}

public class MonthlyConsumptionPoint
{
    public string Month { get; set; } = string.Empty;   // e.g. "Oct 2026"
    public decimal Units { get; set; }
    public decimal Bill { get; set; }
}

public class ConnectionSummary
{
    public int ConnectionId { get; set; }
    public string ConsumerNumber { get; set; } = string.Empty;
    public string ConnectionType { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public decimal? LatestUnits { get; set; }
    public decimal? LatestBill { get; set; }
    public DateTime? LatestReadingDate { get; set; }
    public bool IsActive { get; set; }
}

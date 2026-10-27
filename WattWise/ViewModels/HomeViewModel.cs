namespace WattWise.ViewModels;

public class HomeViewModel
{
    public string AppVersion { get; set; } = "1.0";
    public string Tagline { get; set; } = "WattWise";
    public List<HomeFeature> Features { get; set; } = new()
    {
        new("📊", "Real-Time Tracking",   "Monitor your electricity consumption month by month with accurate meter reading data."),
        new("💡", "Bill Estimation",       "Get instant bill estimates using tiered tariff slabs before your official bill arrives."),
        new("🔌", "Multi-Connection",      "Manage multiple electricity connections from one dashboard — home, office, or shop."),
        new("📈", "Usage Analytics",       "Visualise 6-month consumption trends with interactive charts and month-over-month comparisons.")
    };
}

public record HomeFeature(string Icon, string Title, string Description);

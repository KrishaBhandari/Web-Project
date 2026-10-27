using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using WattWise.Data;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString),
        mySqlOptions => mySqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null)
    ));

// Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath        = "/Account/Login";
        options.LogoutPath       = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan   = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly  = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

// Application services
builder.Services.AddScoped<WattWise.Services.IBillingService, WattWise.Services.BillingService>();
builder.Services.AddScoped<WattWise.Services.PdfReportService>();

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

// Configure QuestPDF community license (free for non-commercial / educational use)
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;


// ── HTTP pipeline ─────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();   // Must come before UseAuthorization
app.UseAuthorization();

app.MapStaticAssets();

// ── Database health-check (dev-only) ─────────────────────────────────────────
app.MapGet("/health", async (ApplicationDbContext db) =>
{
    try
    {
        await db.Database.ExecuteSqlRawAsync("SELECT 1");
        return Results.Ok(new
        {
            status    = "Healthy",
            database  = "Connected",
            query     = "SELECT 1 executed successfully",
            timestamp = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Database health check failed: {Message}", ex.Message);
        return Results.Problem(
            detail: "Cannot connect to MySQL. Ensure XAMPP is running on port 3307.",
            statusCode: 503);
    }
});

// ── Default MVC route ────────────────────────────────────────────────────────
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

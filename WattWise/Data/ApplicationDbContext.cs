using Microsoft.EntityFrameworkCore;
using WattWise.Models;

namespace WattWise.Data;

/// <summary>
/// The central EF Core DbContext for WattWise.
/// All database communication goes through this class.
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // ── DbSets ────────────────────────────────────────────────────────────────
    public DbSet<User>         Users         { get; set; } = null!;
    public DbSet<Connection>   Connections   { get; set; } = null!;
    public DbSet<MeterReading> MeterReadings { get; set; } = null!;
    public DbSet<TariffSlab>   TariffSlabs   { get; set; } = null!;

    // ── Fluent API configuration ──────────────────────────────────────────────
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── User ──────────────────────────────────────────────────────────────
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();        // Email must be unique across users
            entity.Property(u => u.FullName).HasMaxLength(100);
            entity.Property(u => u.Email).HasMaxLength(150);
            entity.Property(u => u.PhoneNumber).HasMaxLength(15);
            entity.Property(u => u.PasswordHash).HasMaxLength(255);
            entity.Property(u => u.Address).HasMaxLength(200);
        });

        // ── Connection ────────────────────────────────────────────────────────
        modelBuilder.Entity<Connection>(entity =>
        {
            entity.HasIndex(c => c.ConsumerNumber).IsUnique();
            entity.Property(c => c.ConsumerNumber).HasMaxLength(20);
            entity.Property(c => c.ConnectionType).HasMaxLength(50);
            entity.Property(c => c.Address).HasMaxLength(200);
            entity.Property(c => c.SanctionedLoad).HasPrecision(8, 2);
            entity.Property(c => c.SolarCapacityKw).HasPrecision(8, 3);

            entity.HasOne(c => c.User)
                  .WithMany(u => u.Connections)
                  .HasForeignKey(c => c.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── MeterReading ──────────────────────────────────────────────────────
        modelBuilder.Entity<MeterReading>(entity =>
        {
            entity.Property(m => m.PreviousReading).HasPrecision(10, 2);
            entity.Property(m => m.CurrentReading).HasPrecision(10, 2);
            entity.Property(m => m.BillAmount).HasPrecision(12, 2);
            entity.Property(m => m.Remarks).HasMaxLength(500);
            // Solar meter reading precision
            entity.Property(m => m.PrevSolarGenReading).HasPrecision(10, 2);
            entity.Property(m => m.CurrSolarGenReading).HasPrecision(10, 2);
            entity.Property(m => m.PrevGridExportReading).HasPrecision(10, 2);
            entity.Property(m => m.CurrGridExportReading).HasPrecision(10, 2);

            entity.HasOne(m => m.Connection)
                  .WithMany(c => c.MeterReadings)
                  .HasForeignKey(m => m.ConnectionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── TariffSlab ────────────────────────────────────────────────────────
        modelBuilder.Entity<TariffSlab>(entity =>
        {
            entity.Property(t => t.ConnectionType).HasMaxLength(50);
            entity.Property(t => t.SlabLabel).HasMaxLength(100);
            entity.Property(t => t.RatePerUnit).HasPrecision(8, 4);
            entity.Property(t => t.FixedCharge).HasPrecision(10, 2);
        });

        // ── Seed default Domestic tariff slabs ───────────────────────────────
        modelBuilder.Entity<TariffSlab>().HasData(
            new TariffSlab
            {
                TariffSlabId   = 1,
                ConnectionType = "Domestic",
                SlabLabel      = "0 – 100 units",
                MinUnits       = 0,
                MaxUnits       = 100,
                RatePerUnit    = 3.50m,
                FixedCharge    = 50.00m,
                IsActive       = true,
                EffectiveFrom  = new DateTime(2024, 1, 1)
            },
            new TariffSlab
            {
                TariffSlabId   = 2,
                ConnectionType = "Domestic",
                SlabLabel      = "101 – 200 units",
                MinUnits       = 101,
                MaxUnits       = 200,
                RatePerUnit    = 5.00m,
                FixedCharge    = 100.00m,
                IsActive       = true,
                EffectiveFrom  = new DateTime(2024, 1, 1)
            },
            new TariffSlab
            {
                TariffSlabId   = 3,
                ConnectionType = "Domestic",
                SlabLabel      = "201 – 500 units",
                MinUnits       = 201,
                MaxUnits       = 500,
                RatePerUnit    = 7.00m,
                FixedCharge    = 150.00m,
                IsActive       = true,
                EffectiveFrom  = new DateTime(2024, 1, 1)
            },
            new TariffSlab
            {
                TariffSlabId   = 4,
                ConnectionType = "Domestic",
                SlabLabel      = "Above 500 units",
                MinUnits       = 501,
                MaxUnits       = null,
                RatePerUnit    = 9.00m,
                FixedCharge    = 200.00m,
                IsActive       = true,
                EffectiveFrom  = new DateTime(2024, 1, 1)
            }
        );
    }
}

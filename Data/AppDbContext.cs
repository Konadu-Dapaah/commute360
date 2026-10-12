using Microsoft.EntityFrameworkCore;
using Commute360.Models;
using Route = Commute360.Models.Route; 

namespace Commute360.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Stop> Stops { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Route> Routes { get; set; } = null!;
    public DbSet<Booking> Bookings { get; set; } = null!;
    public DbSet<BusLocation> BusLocations { get; set; } = null!;
    public DbSet<DriverAlert> DriverAlerts { get; set; } = null!;
    public DbSet<WaitlistEntry> WaitlistEntries { get; set; } = null!;
    public DbSet<Announcement> Announcements { get; set; } = null!;
    public DbSet<Boarding> Boardings { get; set; } = null!;
    public DbSet<SkippedRide> SkippedRides { get; set; } = null!;
    public DbSet<EmailToken> EmailTokens { get; set; } = null!;
    public DbSet<SosIncident> SosIncidents { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BusLocation>().HasIndex(l => l.RouteId).IsUnique();
        modelBuilder.Entity<DriverAlert>().HasIndex(a => new { a.RouteId, a.CreatedAt });

        // One skip and one boarding record per booking per date
        modelBuilder.Entity<SkippedRide>().HasIndex(s => new { s.BookingId, s.RideDate }).IsUnique();
        modelBuilder.Entity<Boarding>().HasIndex(b => new { b.BookingId, b.RideDate }).IsUnique();

        modelBuilder.Entity<WaitlistEntry>().HasIndex(w => new { w.RouteId, w.Status, w.CreatedAt });
        modelBuilder.Entity<EmailToken>().HasIndex(t => t.TokenHash).IsUnique();
        modelBuilder.Entity<Announcement>().HasIndex(a => a.CreatedAt);
        modelBuilder.Entity<SosIncident>().HasIndex(s => new { s.ResolvedAt, s.CreatedAt });

        modelBuilder.Entity<Booking>()
            .HasOne(b => b.BoardingStop)
            .WithMany()
            .HasForeignKey(b => b.BoardingStopId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Booking>()
            .HasOne(b => b.DropOffStop)
            .WithMany()
            .HasForeignKey(b => b.DropOffStopId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
//  DO NOT PUT ANY "public class Announcement", "public class WaitlistEntry", ETC. DOWN HERE!using Microsoft.EntityFrameworkCore;

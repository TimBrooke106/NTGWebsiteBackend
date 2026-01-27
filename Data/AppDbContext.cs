using Microsoft.EntityFrameworkCore;
using SkipHire.Api.Models;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace SkipHire.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Booking>()
            .Property(b => b.Status)
            .HasMaxLength(20)
            .HasDefaultValue("Pending");

        // Unique per day + timeslot
        modelBuilder.Entity<Booking>()
            .HasIndex(b => new { b.PreferredDate, b.TimeSlot })
            .IsUnique();

        base.OnModelCreating(modelBuilder);
    }
}

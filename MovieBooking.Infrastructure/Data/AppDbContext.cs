using Microsoft.EntityFrameworkCore;
using MovieBooking.Core.Entities;

namespace MovieBooking.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Show> Shows { get; set; }
    public DbSet<Seat> Seats { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Show
        modelBuilder.Entity<Show>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired();
        });

        // Configure Seat
        modelBuilder.Entity<Seat>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Row).IsRequired();
            
            // Configure Optimistic Concurrency
            entity.Property(e => e.RowVersion)
                .IsConcurrencyToken(); // Use ConcurrencyToken for SQLite manual handling
        });
    }

    public override int SaveChanges()
    {
        UpdateRowVersions();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateRowVersions();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateRowVersions()
    {
        foreach (var entry in ChangeTracker.Entries<Seat>())
        {
            if (entry.State == EntityState.Modified || entry.State == EntityState.Added)
            {
                // Manually set RowVersion for SQLite
                entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
            }
        }
    }
}

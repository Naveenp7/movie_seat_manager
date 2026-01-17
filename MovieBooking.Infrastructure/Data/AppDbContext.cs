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
                .IsRowVersion();
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
            if (entry.State == EntityState.Modified)
            {
                // Manually increment/change RowVersion for SQLite simulation
                // In SQL Server, this is ignored as the DB handles it.
                // But EF Core might expect us to provide a value if we map it? 
                // Actually, for SQLite, we just need the value to change.
                entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
            }
        }
    }
}

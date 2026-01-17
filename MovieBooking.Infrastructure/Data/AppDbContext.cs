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

        modelBuilder.Entity<Show>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired();
            entity.HasMany(s => s.Seats)
                  .WithOne(s => s.Show)
                  .HasForeignKey(s => s.ShowId);
        });

        modelBuilder.Entity<Seat>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Row).IsRequired();
            entity.Property(s => s.RowVersion).IsConcurrencyToken();
        });
    }
    
    // Postgres doesn't need manual RowVersion incrementing like SQLite specific trickery
    // We can rely on application-side versioning or database triggers. 
    // To keep it simple and robust: We will increment the RowVersion manually in SaveChanges
    // to ensure it changes on every update, acting as an application-managed concurrency token.

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
                entry.Entity.RowVersion = Guid.NewGuid().ToByteArray(); 
            }
            else if (entry.State == EntityState.Added)
            {
                entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
            }
        }
    }
}

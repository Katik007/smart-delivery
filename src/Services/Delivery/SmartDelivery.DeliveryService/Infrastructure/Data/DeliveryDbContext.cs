using Microsoft.EntityFrameworkCore;
using SmartDelivery.DeliveryService.Domain;

namespace SmartDelivery.DeliveryService.Infrastructure.Data;

public class DeliveryDbContext : DbContext
{
    public DeliveryDbContext(DbContextOptions<DeliveryDbContext> options) : base(options) { }

    public DbSet<DeliveryTask> DeliveryTasks => Set<DeliveryTask>();
    public DbSet<Courier> Couriers => Set<Courier>();
    public DbSet<DeliveryRoute> DeliveryRoutes => Set<DeliveryRoute>();
    public DbSet<RouteStop> RouteStops => Set<RouteStop>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DeliveryTask>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.DeliveryAddress).IsRequired().HasMaxLength(500);
            entity.Property(t => t.TotalAmount).HasPrecision(18, 2);
            entity.Property(t => t.FailureReason).HasMaxLength(500);
            entity.HasIndex(t => t.OrderId).IsUnique();
            entity.HasIndex(t => t.Status);
            entity.HasIndex(t => t.CourierId);
        });

        modelBuilder.Entity<Courier>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.FullName).IsRequired().HasMaxLength(150);
            entity.Property(c => c.PhoneNumber).IsRequired().HasMaxLength(50);
            entity.Property(c => c.CurrentZone).HasMaxLength(100);
            entity.HasIndex(c => c.Status);
        });

        modelBuilder.Entity<DeliveryRoute>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => r.CourierId);
            entity.HasMany(r => r.Stops)
                  .WithOne()
                  .HasForeignKey(s => s.RouteId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RouteStop>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Address).IsRequired().HasMaxLength(500);
            entity.HasIndex(s => s.RouteId);
        });

        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Type).IsRequired().HasMaxLength(200);
            entity.Property(m => m.Payload).IsRequired();
        });
    }
}

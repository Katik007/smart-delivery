using Microsoft.EntityFrameworkCore;
using SmartDelivery.OrderService.Domain;

namespace SmartDelivery.OrderService.Infrastructure.Data;

public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<OrderSagaState> SagaStates => Set<OrderSagaState>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.DeliveryAddress).IsRequired().HasMaxLength(500);
            entity.Property(o => o.TotalAmount).HasPrecision(18, 2);
            entity.Property(o => o.CancellationReason).HasMaxLength(500);

            entity.HasMany(o => o.Items)
                  .WithOne()
                  .HasForeignKey(i => i.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(o => o.CustomerId);
            entity.HasIndex(o => o.Status);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Title).IsRequired().HasMaxLength(200);
            entity.Property(i => i.UnitPrice).HasPrecision(18, 2);
            entity.HasIndex(i => i.OrderId);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Amount).HasPrecision(18, 2);
            entity.Property(p => p.TransactionReference).HasMaxLength(100);
            entity.Property(p => p.FailureReason).HasMaxLength(500);
            entity.HasIndex(p => p.OrderId);
        });

        modelBuilder.Entity<OrderSagaState>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.CurrentStep).HasMaxLength(100);
            entity.Property(s => s.ErrorMessage).HasMaxLength(500);
            entity.HasIndex(s => s.OrderId).IsUnique();
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Type).IsRequired().HasMaxLength(200);
            entity.Property(m => m.Content).IsRequired();
            entity.HasIndex(m => m.ProcessedOnUtc);
        });
    }
}

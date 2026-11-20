using Microsoft.EntityFrameworkCore;
using SmartDelivery.CatalogService.Domain;

namespace SmartDelivery.CatalogService.Infrastructure.Data;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.HasKey(w => w.Id);
            entity.Property(w => w.Code).IsRequired().HasMaxLength(50);
            entity.Property(w => w.Name).IsRequired().HasMaxLength(200);
            entity.Property(w => w.Address).HasMaxLength(500);
            entity.HasIndex(w => w.Code).IsUnique();
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
            entity.Property(p => p.Description).HasMaxLength(2000);
            entity.Property(p => p.Price).HasPrecision(18, 2);

            entity.HasOne(p => p.Inventory)
                  .WithOne()
                  .HasForeignKey<InventoryItem>(i => i.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(p => p.CategoryId);
            entity.HasIndex(p => p.SellerId);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(150);
            entity.Property(c => c.Description).HasMaxLength(1000);
        });

        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.WarehouseLocation).HasMaxLength(100);
            entity.HasIndex(i => i.ProductId).IsUnique();
        });
    }
}

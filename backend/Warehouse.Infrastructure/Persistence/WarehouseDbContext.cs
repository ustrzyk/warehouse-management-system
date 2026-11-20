
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Warehouse.Domain.Entities;
using Warehouse.Infrastructure.Identity;
using WarehouseEntity = global::Warehouse.Domain.Entities.Warehouse;

namespace Warehouse.Infrastructure.Persistence;

public class WarehouseDbContext : IdentityDbContext<ApplicationUser>
{
    public WarehouseDbContext(
        DbContextOptions<WarehouseDbContext> options)
        : base(options)
    {
    }

    // Warehouse structure
    public DbSet<WarehouseEntity> Warehouses
        => Set<WarehouseEntity>();

    public DbSet<WarehouseZone> WarehouseZones
        => Set<WarehouseZone>();

    public DbSet<StorageLocation> StorageLocations
        => Set<StorageLocation>();

    // Product catalog
    public DbSet<ProductCategory> ProductCategories
        => Set<ProductCategory>();

    public DbSet<UnitOfMeasure> UnitsOfMeasure
        => Set<UnitOfMeasure>();

    public DbSet<Product> Products
        => Set<Product>();

    public DbSet<ProductBatch> ProductBatches
        => Set<ProductBatch>();

    // Logistic units
    public DbSet<LogisticUnit> LogisticUnits
        => Set<LogisticUnit>();

    public DbSet<LogisticUnitItem> LogisticUnitItems
        => Set<LogisticUnitItem>();

    public DbSet<LogisticUnitMovement> LogisticUnitMovements
        => Set<LogisticUnitMovement>();

    // Goods receipts
    public DbSet<GoodsReceipt> GoodsReceipts
        => Set<GoodsReceipt>();

    public DbSet<GoodsReceiptItem> GoodsReceiptItems
        => Set<GoodsReceiptItem>();

    // Warehouse orders
    public DbSet<WarehouseOrder> WarehouseOrders
        => Set<WarehouseOrder>();

    public DbSet<WarehouseOrderItem> WarehouseOrderItems
        => Set<WarehouseOrderItem>();

    // Picking
    public DbSet<PickingOperation> PickingOperations
        => Set<PickingOperation>();

    // Shipping
    public DbSet<Shipment> Shipments
        => Set<Shipment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Additional user properties
        modelBuilder.Entity<ApplicationUser>(builder =>
        {
            builder.Property(x => x.FullName)
                .HasMaxLength(150)
                .IsRequired();

            builder.Property(x => x.IsActive)
                .HasDefaultValue(true)
                .IsRequired();
        });

        // Existing warehouse configurations
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(WarehouseDbContext).Assembly);
    }
}

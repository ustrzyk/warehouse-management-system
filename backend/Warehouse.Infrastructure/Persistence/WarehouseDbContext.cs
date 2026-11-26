
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

    // Trzymam encje struktury magazynu.
    public DbSet<WarehouseEntity> Warehouses
        => Set<WarehouseEntity>();

    public DbSet<WarehouseZone> WarehouseZones
        => Set<WarehouseZone>();

    public DbSet<StorageLocation> StorageLocations
        => Set<StorageLocation>();

    // Trzymam encje katalogu produktów.
    public DbSet<ProductCategory> ProductCategories
        => Set<ProductCategory>();

    public DbSet<UnitOfMeasure> UnitsOfMeasure
        => Set<UnitOfMeasure>();

    public DbSet<Product> Products
        => Set<Product>();

    public DbSet<ProductBatch> ProductBatches
        => Set<ProductBatch>();

    // Trzymam encje jednostek logistycznych.
    public DbSet<LogisticUnit> LogisticUnits
        => Set<LogisticUnit>();

    public DbSet<LogisticUnitItem> LogisticUnitItems
        => Set<LogisticUnitItem>();

    public DbSet<LogisticUnitMovement> LogisticUnitMovements
        => Set<LogisticUnitMovement>();

    // Trzymam encje przyjęć towaru.
    public DbSet<GoodsReceipt> GoodsReceipts
        => Set<GoodsReceipt>();

    public DbSet<GoodsReceiptItem> GoodsReceiptItems
        => Set<GoodsReceiptItem>();

    // Trzymam encje zleceń magazynowych.
    public DbSet<WarehouseOrder> WarehouseOrders
        => Set<WarehouseOrder>();

    public DbSet<WarehouseOrderItem> WarehouseOrderItems
        => Set<WarehouseOrderItem>();

    // Trzymam encje operacji kompletacji.
    public DbSet<PickingOperation> PickingOperations
        => Set<PickingOperation>();

    // Trzymam encje wysyłek.
    public DbSet<Shipment> Shipments
        => Set<Shipment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Dodatkowo konfiguruję właściwości użytkownika.
        modelBuilder.Entity<ApplicationUser>(builder =>
        {
            builder.Property(x => x.FullName)
                .HasMaxLength(150)
                .IsRequired();

            builder.Property(x => x.IsActive)
                .HasDefaultValue(true)
                .IsRequired();
        });

        // Podpinam wszystkie konfiguracje encji z assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(WarehouseDbContext).Assembly);
    }
}

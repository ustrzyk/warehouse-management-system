
using Microsoft.EntityFrameworkCore;
using Warehouse.Domain.Entities;
using WarehouseEntity = global::Warehouse.Domain.Entities.Warehouse;

namespace Warehouse.Infrastructure.Persistence;

public class WarehouseDbContext : DbContext
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(WarehouseDbContext).Assembly);
    }
}

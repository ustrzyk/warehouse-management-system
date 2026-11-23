
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.DTOs.Shipments;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Services;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;
using Warehouse.Infrastructure.Persistence;
using Warehouse.Infrastructure.Repositories;
using WarehouseEntity = global::Warehouse.Domain.Entities.Warehouse;
using Xunit;

namespace Warehouse.Tests.Integration;

[Trait("Category", "SqlServer")]
public class SqlServerShipmentIntegrationTests
{
    private static readonly CancellationToken Cancellation =
        CancellationToken.None;

    // ---------- Creating shipments ----------

    [Fact]
    public async Task Create_CompletedOrder_PersistsPreparedShipment()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            int orderId;
            int shipmentId;

            await using (var setup = new WarehouseDbContext(options))
            {
                orderId = await SeedOrderAsync(setup);
            }

            await using (var context = new WarehouseDbContext(options))
            {
                var service = CreateService(context);

                var result = await service.CreateAsync(
                    Request(orderId, "  DHL  ", "  TRACK-123  "),
                    Cancellation);

                shipmentId = result.Id;

                Assert.True(shipmentId > 0);
                Assert.Equal($"SHP-{orderId:D8}", result.Number);
                Assert.Equal(orderId, result.WarehouseOrderId);
                Assert.Equal("ORD-SHIP-001", result.WarehouseOrderNumber);
                Assert.Equal("DHL", result.CarrierName);
                Assert.Equal("TRACK-123", result.TrackingNumber);
                Assert.Equal("Prepared", result.Status);
                Assert.Null(result.ShippedAtUtc);
                Assert.Equal(0, result.Revision);
            }

            await using var verification =
                new WarehouseDbContext(options);

            var repository = new ShipmentRepository(verification);

            var saved = await repository.GetByIdAsync(
                shipmentId, Cancellation);

            Assert.NotNull(saved);
            Assert.Equal(ShipmentStatus.Prepared, saved!.Status);
            Assert.Equal("DHL", saved.CarrierName);
            Assert.Equal("TRACK-123", saved.TrackingNumber);
            Assert.NotEmpty(saved.RowVersion);

            Assert.True(await repository.ExistsForOrderAsync(
                orderId, Cancellation));

            var byOrder = await repository.GetByOrderIdAsync(
                orderId, Cancellation);

            Assert.NotNull(byOrder);
            Assert.Equal(shipmentId, byOrder!.Id);
            Assert.Equal("ORD-SHIP-001", byOrder.WarehouseOrder.Number);
        });
    }

    [Fact]
    public async Task Create_BlankTrackingNumber_SavesNull()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            int orderId;

            await using (var setup = new WarehouseDbContext(options))
            {
                orderId = await SeedOrderAsync(setup);
            }

            await using (var context = new WarehouseDbContext(options))
            {
                var result = await CreateService(context).CreateAsync(
                    Request(orderId, "DPD", "   "),
                    Cancellation);

                Assert.Equal("DPD", result.CarrierName);
                Assert.Null(result.TrackingNumber);
            }

            await using var verification =
                new WarehouseDbContext(options);

            var saved = await verification.Shipments
                .AsNoTracking()
                .SingleAsync();

            Assert.Null(saved.TrackingNumber);
        });
    }

    [Fact]
    public async Task Create_BlankCarrier_RejectsWithoutSaving()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            int orderId;

            await using (var setup = new WarehouseDbContext(options))
            {
                orderId = await SeedOrderAsync(setup);
            }

            await using (var context = new WarehouseDbContext(options))
            {
                await Assert.ThrowsAsync<InvalidShipmentException>(
                    () => CreateService(context).CreateAsync(
                        Request(orderId, "   "),
                        Cancellation));
            }

            await using var verification =
                new WarehouseDbContext(options);

            Assert.Equal(
                0,
                await verification.Shipments.CountAsync());
        });
    }

    [Theory]
    [InlineData(WarehouseOrderStatus.Draft)]
    [InlineData(WarehouseOrderStatus.Released)]
    [InlineData(WarehouseOrderStatus.Shipped)]
    public async Task Create_OrderNotCompleted_RejectsShipment(
        WarehouseOrderStatus status)
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            int orderId;

            await using (var setup = new WarehouseDbContext(options))
            {
                orderId = await SeedOrderAsync(setup, status);
            }

            await using (var context = new WarehouseDbContext(options))
            {
                await Assert.ThrowsAsync<InvalidShipmentException>(
                    () => CreateService(context).CreateAsync(
                        Request(orderId, "DHL"),
                        Cancellation));
            }

            await using var verification =
                new WarehouseDbContext(options);

            Assert.Equal(
                0,
                await verification.Shipments.CountAsync());

            var order = await verification.WarehouseOrders
                .AsNoTracking()
                .SingleAsync(x => x.Id == orderId);

            Assert.Equal(status, order.Status);
            Assert.Equal(0, order.Revision);
        });
    }

    // ---------- Duplicate shipment protection ----------

    [Fact]
    public async Task Create_SecondShipmentForOrder_ThrowsConflict()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            int orderId;

            await using (var setup = new WarehouseDbContext(options))
            {
                orderId = await SeedOrderAsync(setup);
            }

            await CreateShipmentAsync(options, orderId);

            await using (var context = new WarehouseDbContext(options))
            {
                await Assert.ThrowsAsync<ShipmentConflictException>(
                    () => CreateService(context).CreateAsync(
                        Request(orderId, "DPD"),
                        Cancellation));
            }

            await using var verification =
                new WarehouseDbContext(options);

            Assert.Equal(
                1,
                await verification.Shipments.CountAsync());

            var saved = await verification.Shipments
                .AsNoTracking()
                .SingleAsync();

            Assert.Equal("DHL", saved.CarrierName);
        });
    }

    [Fact]
    public async Task Add_DuplicateShipmentInSqlServer_ThrowsConflict()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            int orderId;

            await using (var setup = new WarehouseDbContext(options))
            {
                orderId = await SeedOrderAsync(setup);
            }

            await CreateShipmentAsync(options, orderId);

            await using (var context = new WarehouseDbContext(options))
            {
                var repository = new ShipmentRepository(context);

                await repository.AddAsync(
                    new Shipment
                    {
                        Number = $"SHP-{orderId:D8}",
                        WarehouseOrderId = orderId,
                        CarrierName = "DPD",
                        Status = ShipmentStatus.Prepared,
                        CreatedAtUtc = DateTime.UtcNow
                    },
                    Cancellation);

                await Assert.ThrowsAsync<ShipmentConflictException>(
                    () => repository.SaveChangesAsync(Cancellation));
            }

            await using var verification =
                new WarehouseDbContext(options);

            Assert.Equal(
                1,
                await verification.Shipments.CountAsync());
        });
    }

    // ---------- Dispatching shipments ----------

    [Fact]
    public async Task Dispatch_PreparedShipment_ShipsOrderAtomically()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            int orderId;
            int stockItemId;

            await using (var setup = new WarehouseDbContext(options))
            {
                orderId = await SeedOrderAsync(setup);
                stockItemId = await SeedStockAsync(setup);
            }

            var shipmentId = await CreateShipmentAsync(
                options, orderId);

            await using (var context = new WarehouseDbContext(options))
            {
                var result = await CreateService(context).DispatchAsync(
                    shipmentId, Cancellation);

                Assert.NotNull(result);
                Assert.Equal("Shipped", result!.Status);
                Assert.NotNull(result.ShippedAtUtc);
                Assert.Equal(1, result.Revision);
            }

            await using var verification =
                new WarehouseDbContext(options);

            var shipment = await verification.Shipments
                .AsNoTracking()
                .SingleAsync(x => x.Id == shipmentId);

            var order = await verification.WarehouseOrders
                .AsNoTracking()
                .SingleAsync(x => x.Id == orderId);

            var stock = await verification.LogisticUnitItems
                .AsNoTracking()
                .SingleAsync(x => x.Id == stockItemId);

            Assert.Equal(ShipmentStatus.Shipped, shipment.Status);
            Assert.Equal(WarehouseOrderStatus.Shipped, order.Status);

            Assert.NotNull(shipment.ShippedAtUtc);
            Assert.Equal(
                shipment.ShippedAtUtc!.Value,
                order.ShippedAtUtc!.Value,
                TimeSpan.FromMilliseconds(1));

            Assert.Equal(1, shipment.Revision);
            Assert.Equal(1, order.Revision);

            // Dispatch does not reduce stock a second time.
            Assert.Equal(20m, stock.Quantity);

            Assert.Equal(
                0,
                await verification.PickingOperations.CountAsync());
        });
    }

    [Fact]
    public async Task Dispatch_AlreadyShipped_ThrowsConflict()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            int orderId;

            await using (var setup = new WarehouseDbContext(options))
            {
                orderId = await SeedOrderAsync(setup);
            }

            var shipmentId = await CreateShipmentAsync(
                options, orderId);

            await using (var first = new WarehouseDbContext(options))
            {
                var result = await CreateService(first).DispatchAsync(
                    shipmentId, Cancellation);

                Assert.NotNull(result);
            }

            await using (var second = new WarehouseDbContext(options))
            {
                await Assert.ThrowsAsync<ShipmentConflictException>(
                    () => CreateService(second).DispatchAsync(
                        shipmentId, Cancellation));
            }

            await using var verification =
                new WarehouseDbContext(options);

            var shipment = await verification.Shipments
                .AsNoTracking()
                .SingleAsync(x => x.Id == shipmentId);

            var order = await verification.WarehouseOrders
                .AsNoTracking()
                .SingleAsync(x => x.Id == orderId);

            Assert.Equal(ShipmentStatus.Shipped, shipment.Status);
            Assert.Equal(WarehouseOrderStatus.Shipped, order.Status);
            Assert.Equal(1, shipment.Revision);
            Assert.Equal(1, order.Revision);
        });
    }

    [Fact]
    public async Task Dispatch_OrderNotCompleted_ThrowsConflict()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            int orderId;

            await using (var setup = new WarehouseDbContext(options))
            {
                orderId = await SeedOrderAsync(setup);
            }

            var shipmentId = await CreateShipmentAsync(
                options, orderId);

            // Change order status before dispatch.

            await using (var modification =
                new WarehouseDbContext(options))
            {
                var order = await modification.WarehouseOrders
                    .SingleAsync(x => x.Id == orderId);

                order.Status = WarehouseOrderStatus.Released;
                order.Revision++;

                await modification.SaveChangesAsync(Cancellation);
            }

            await using (var dispatchContext =
                new WarehouseDbContext(options))
            {
                await Assert.ThrowsAsync<ShipmentConflictException>(
                    () => CreateService(dispatchContext).DispatchAsync(
                        shipmentId, Cancellation));
            }

            await using var verification =
                new WarehouseDbContext(options);

            var shipment = await verification.Shipments
                .AsNoTracking()
                .SingleAsync(x => x.Id == shipmentId);

            var orderAfter = await verification.WarehouseOrders
                .AsNoTracking()
                .SingleAsync(x => x.Id == orderId);

            Assert.Equal(ShipmentStatus.Prepared, shipment.Status);
            Assert.Null(shipment.ShippedAtUtc);
            Assert.Equal(0, shipment.Revision);

            Assert.Equal(WarehouseOrderStatus.Released, orderAfter.Status);
            Assert.Equal(1, orderAfter.Revision);
        });
    }

    // ---------- SQL Server concurrency ----------

    [Fact]
    public async Task Dispatch_StaleShipmentRowVersion_ThrowsConcurrency()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            int orderId;

            await using (var setup = new WarehouseDbContext(options))
            {
                orderId = await SeedOrderAsync(setup);
            }

            var shipmentId = await CreateShipmentAsync(
                options, orderId);

            await using var contextA =
                new WarehouseDbContext(options);

            await using var contextB =
                new WarehouseDbContext(options);

            var serviceA = CreateService(contextA);
            var serviceB = CreateService(contextB);

            var repositoryA = new ShipmentRepository(contextA);
            var repositoryB = new ShipmentRepository(contextB);

            // Both contexts load the same initial database state.

            var shipmentA = await repositoryA.GetTrackedByIdAsync(
                shipmentId, Cancellation);

            var shipmentB = await repositoryB.GetTrackedByIdAsync(
                shipmentId, Cancellation);

            Assert.NotNull(shipmentA);
            Assert.NotNull(shipmentB);

            var originalVersion = shipmentB!.RowVersion.ToArray();

            // First operation succeeds.

            var first = await serviceA.DispatchAsync(
                shipmentId, Cancellation);

            Assert.NotNull(first);
            Assert.Equal("Shipped", first!.Status);

            // Second context still has the old RowVersion.

            await Assert.ThrowsAsync<
                ConcurrentShipmentModificationException>(
                () => serviceB.DispatchAsync(
                    shipmentId, Cancellation));

            await using var verification =
                new WarehouseDbContext(options);

            var actualShipment = await verification.Shipments
                .AsNoTracking()
                .SingleAsync(x => x.Id == shipmentId);

            var actualOrder = await verification.WarehouseOrders
                .AsNoTracking()
                .SingleAsync(x => x.Id == orderId);

            Assert.Equal(ShipmentStatus.Shipped, actualShipment.Status);
            Assert.Equal(WarehouseOrderStatus.Shipped, actualOrder.Status);

            Assert.Equal(1, actualShipment.Revision);
            Assert.Equal(1, actualOrder.Revision);

            Assert.False(
                originalVersion.SequenceEqual(
                    actualShipment.RowVersion));

            Assert.Equal(
                1,
                await verification.Shipments.CountAsync());
        });
    }

    // ---------- Atomic rollback on order concurrency ----------

    [Fact]
    public async Task Dispatch_StaleOrder_RollsBackShipmentChanges()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            int orderId;

            await using (var setup = new WarehouseDbContext(options))
            {
                orderId = await SeedOrderAsync(setup);
            }

            var shipmentId = await CreateShipmentAsync(
                options, orderId);

            await using var staleContext =
                new WarehouseDbContext(options);

            var staleRepository =
                new ShipmentRepository(staleContext);

            var staleShipment = await staleRepository
                .GetTrackedByIdAsync(shipmentId, Cancellation);

            Assert.NotNull(staleShipment);

            // Another operation changes only the order.

            await using (var freshContext =
                new WarehouseDbContext(options))
            {
                var order = await freshContext.WarehouseOrders
                    .SingleAsync(x => x.Id == orderId);

                order.Revision++;

                await freshContext.SaveChangesAsync(Cancellation);
            }

            // Dispatch uses an outdated order RowVersion.

            await Assert.ThrowsAsync<
                ConcurrentShipmentModificationException>(
                () => CreateService(staleContext).DispatchAsync(
                    shipmentId, Cancellation));

            // Neither shipment status nor its revision
            // may be partially saved.

            await using var verification =
                new WarehouseDbContext(options);

            var shipment = await verification.Shipments
                .AsNoTracking()
                .SingleAsync(x => x.Id == shipmentId);

            var orderAfter = await verification.WarehouseOrders
                .AsNoTracking()
                .SingleAsync(x => x.Id == orderId);

            Assert.Equal(ShipmentStatus.Prepared, shipment.Status);
            Assert.Null(shipment.ShippedAtUtc);
            Assert.Equal(0, shipment.Revision);

            Assert.Equal(WarehouseOrderStatus.Completed, orderAfter.Status);
            Assert.Null(orderAfter.ShippedAtUtc);
            Assert.Equal(1, orderAfter.Revision);
        });
    }

    // ---------- Helpers ----------

    private static ShipmentService CreateService(
        WarehouseDbContext context)
    {
        return new ShipmentService(
            new ShipmentRepository(context));
    }

    private static CreateShipmentRequest Request(
        int orderId,
        string carrier,
        string? tracking = null)
    {
        return new CreateShipmentRequest
        {
            WarehouseOrderId = orderId,
            CarrierName = carrier,
            TrackingNumber = tracking
        };
    }

    private static async Task<int> SeedOrderAsync(
        WarehouseDbContext context,
        WarehouseOrderStatus status = WarehouseOrderStatus.Completed)
    {
        var warehouse = new WarehouseEntity
        {
            Code = "WH-SHIP-001",
            Name = "Shipment Test Warehouse",
            IsActive = true
        };

        var order = new WarehouseOrder
        {
            Number = "ORD-SHIP-001",
            Warehouse = warehouse,
            CustomerName = "Integration Customer",
            Status = status,
            CreatedAtUtc = DateTime.UtcNow,
            CompletedAtUtc =
                status == WarehouseOrderStatus.Completed
                    ? DateTime.UtcNow
                    : null,
            Revision = 0
        };

        context.WarehouseOrders.Add(order);

        await context.SaveChangesAsync(Cancellation);

        return order.Id;
    }

    private static async Task<int> SeedStockAsync(
        WarehouseDbContext context)
    {
        var warehouse = await context.Warehouses.SingleAsync();

        var zone = new WarehouseZone
        {
            Warehouse = warehouse,
            Code = "ZONE-SHIP",
            Name = "Shipping Test Zone",
            IsActive = true
        };

        var location = new StorageLocation
        {
            WarehouseZone = zone,
            Code = "A-01",
            IsActive = true
        };

        var category = new ProductCategory
        {
            Code = "CAT-SHIP",
            Name = "Shipping Test Category",
            IsActive = true
        };

        var unitOfMeasure = new UnitOfMeasure
        {
            Code = "PCS",
            Name = "Pieces",
            Symbol = "pcs",
            IsActive = true
        };

        var product = new Product
        {
            Code = "ART-SHIP-001",
            Name = "Shipping Test Product",
            ProductCategory = category,
            UnitOfMeasure = unitOfMeasure,
            IsActive = true
        };

        var item = new LogisticUnitItem
        {
            Product = product,
            Quantity = 20m
        };

        var pallet = new LogisticUnit
        {
            Code = "LE-SHIP-001",
            Status = LogisticUnitStatus.Stored,
            StorageLocation = location,
            CreatedAtUtc = DateTime.UtcNow,
            Items = new List<LogisticUnitItem> { item }
        };

        context.LogisticUnits.Add(pallet);

        await context.SaveChangesAsync(Cancellation);

        return item.Id;
    }

    private static async Task<int> CreateShipmentAsync(
        DbContextOptions<WarehouseDbContext> options,
        int orderId)
    {
        await using var context = new WarehouseDbContext(options);

        var result = await CreateService(context).CreateAsync(
            Request(orderId, "DHL", "TRACK-123"),
            Cancellation);

        return result.Id;
    }

    private static async Task RunWithTemporaryDatabaseAsync(
        Func<DbContextOptions<WarehouseDbContext>, Task> test)
    {
        // Each test uses a different temporary SQL Server database.

        var databaseName =
            "WarehouseIntegration_" + Guid.NewGuid().ToString("N");

        var connectionString = new SqlConnectionStringBuilder
        {
            DataSource = @"(localdb)\MSSQLLocalDB",
            InitialCatalog = databaseName,
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            ConnectTimeout = 30
        }.ConnectionString;

        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        var migrationCompleted = false;

        try
        {
            await using (var setup =
                new WarehouseDbContext(options))
            {
                await setup.Database.MigrateAsync();
                migrationCompleted = true;
            }

            await test(options);
        }
        finally
        {
            if (migrationCompleted)
            {
                // Only delete the database generated for this test.

                await using var cleanup =
                    new WarehouseDbContext(options);

                await cleanup.Database.EnsureDeletedAsync();
            }
        }
    }
}

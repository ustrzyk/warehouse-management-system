
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.DTOs.Picking;
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
public class SqlServerPickingIntegrationTests
{
    private static readonly CancellationToken Cancellation =
        CancellationToken.None;

    // ---------- 1. Partial picking ----------

    [Fact]
    public async Task Create_PartialPick_ReducesStockAndSavesHistory()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            SeedData seed;
            Guid requestId = Guid.NewGuid();

            await using (var setup = new WarehouseDbContext(options))
            {
                seed = await SeedAsync(setup);
            }

            await using (var context = new WarehouseDbContext(options))
            {
                var service = CreateService(context);

                var result = await service.CreateAsync(
                    seed.OrderId,
                    Request(seed, 4.5m, requestId),
                    Cancellation);

                Assert.NotNull(result);
                Assert.True(result!.Id > 0);
                Assert.Equal(requestId, result.RequestId);
                Assert.Equal(4.5m, result.Quantity);
                Assert.Equal("LE-PICK-001", result.LogisticUnitCode);
                Assert.Equal("ART-PICK-001", result.ProductCode);
                Assert.Equal("BATCH-001", result.BatchNumber);
                Assert.Equal("A-01", result.SourceLocationCode);
            }

            await using var verification =
                new WarehouseDbContext(options);

            var palletItem = await verification.LogisticUnitItems
                .AsNoTracking()
                .SingleAsync(x => x.Id == seed.PalletItemId);

            var pallet = await verification.LogisticUnits
                .AsNoTracking()
                .SingleAsync(x => x.Id == seed.PalletId);

            var order = await verification.WarehouseOrders
                .AsNoTracking()
                .SingleAsync(x => x.Id == seed.OrderId);

            var operation = await verification.PickingOperations
                .AsNoTracking()
                .SingleAsync();

            Assert.Equal(15.5m, palletItem.Quantity);
            Assert.Equal(1, pallet.Revision);
            Assert.Equal(1, order.Revision);
            Assert.Equal(WarehouseOrderStatus.Released, order.Status);
            Assert.Null(order.CompletedAtUtc);
            Assert.Equal(4.5m, operation.Quantity);
            Assert.Equal(seed.OrderItemId, operation.WarehouseOrderItemId);
        });
    }

    // ---------- 2. Completing an order ----------

    [Fact]
    public async Task Create_FinalPick_CompletesOrder()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            SeedData seed;

            await using (var setup = new WarehouseDbContext(options))
            {
                seed = await SeedAsync(setup);
            }

            await using (var context = new WarehouseDbContext(options))
            {
                var service = CreateService(context);

                var result = await service.CreateAsync(
                    seed.OrderId,
                    Request(seed, 10m),
                    Cancellation);

                Assert.NotNull(result);
                Assert.Equal(10m, result!.Quantity);
            }

            await using var verification =
                new WarehouseDbContext(options);

            var order = await verification.WarehouseOrders
                .AsNoTracking()
                .SingleAsync(x => x.Id == seed.OrderId);

            var palletItem = await verification.LogisticUnitItems
                .AsNoTracking()
                .SingleAsync(x => x.Id == seed.PalletItemId);

            var operation = await verification.PickingOperations
                .AsNoTracking()
                .SingleAsync();

            Assert.Equal(WarehouseOrderStatus.Completed, order.Status);
            Assert.NotNull(order.CompletedAtUtc);
            Assert.Equal(operation.PickedAtUtc, order.CompletedAtUtc);
            Assert.Equal(10m, palletItem.Quantity);
            Assert.Equal(1, order.Revision);
        });
    }

    // ---------- 3. Idempotency ----------

    [Fact]
    public async Task Create_SameRequestTwice_DoesNotPickTwice()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            SeedData seed;
            var requestId = Guid.NewGuid();

            await using (var setup = new WarehouseDbContext(options))
            {
                seed = await SeedAsync(setup);
            }

            int firstOperationId;

            await using (var firstContext =
                new WarehouseDbContext(options))
            {
                var service = CreateService(firstContext);

                var first = await service.CreateAsync(
                    seed.OrderId,
                    Request(seed, 4m, requestId),
                    Cancellation);

                Assert.NotNull(first);
                firstOperationId = first!.Id;
            }

            await using (var secondContext =
                new WarehouseDbContext(options))
            {
                var service = CreateService(secondContext);

                var second = await service.CreateAsync(
                    seed.OrderId,
                    Request(seed, 4m, requestId),
                    Cancellation);

                Assert.NotNull(second);
                Assert.Equal(firstOperationId, second!.Id);
                Assert.Equal(requestId, second.RequestId);
            }

            await using var verification =
                new WarehouseDbContext(options);

            Assert.Equal(
                1,
                await verification.PickingOperations.CountAsync());

            var palletItem = await verification.LogisticUnitItems
                .AsNoTracking()
                .SingleAsync(x => x.Id == seed.PalletItemId);

            var order = await verification.WarehouseOrders
                .AsNoTracking()
                .SingleAsync(x => x.Id == seed.OrderId);

            Assert.Equal(16m, palletItem.Quantity);
            Assert.Equal(1, order.Revision);
        });
    }

    // ---------- 4. Reusing RequestId incorrectly ----------

    [Fact]
    public async Task Create_SameRequestIdDifferentQuantity_ThrowsConflict()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            SeedData seed;
            var requestId = Guid.NewGuid();

            await using (var setup = new WarehouseDbContext(options))
            {
                seed = await SeedAsync(setup);
            }

            await using (var firstContext =
                new WarehouseDbContext(options))
            {
                var result = await CreateService(firstContext)
                    .CreateAsync(
                        seed.OrderId,
                        Request(seed, 3m, requestId),
                        Cancellation);

                Assert.NotNull(result);
            }

            await using (var secondContext =
                new WarehouseDbContext(options))
            {
                await Assert.ThrowsAsync<PickingConflictException>(
                    () => CreateService(secondContext).CreateAsync(
                        seed.OrderId,
                        Request(seed, 4m, requestId),
                        Cancellation));
            }

            await using var verification =
                new WarehouseDbContext(options);

            Assert.Equal(
                1,
                await verification.PickingOperations.CountAsync());

            Assert.Equal(
                17m,
                await verification.LogisticUnitItems
                    .Where(x => x.Id == seed.PalletItemId)
                    .Select(x => x.Quantity)
                    .SingleAsync());
        });
    }

    // ---------- 5. Cannot exceed ordered quantity ----------

    [Fact]
    public async Task Create_TooMuchForOrder_RejectsWithoutChanges()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            SeedData seed;

            await using (var setup = new WarehouseDbContext(options))
            {
                seed = await SeedAsync(setup);
            }

            await using (var context = new WarehouseDbContext(options))
            {
                await Assert.ThrowsAsync<PickingConflictException>(
                    () => CreateService(context).CreateAsync(
                        seed.OrderId,
                        Request(seed, 11m),
                        Cancellation));
            }

            await AssertUnchangedAsync(options, seed, 20m);
        });
    }

    // ---------- 6. Cannot exceed available pallet stock ----------

    [Fact]
    public async Task Create_TooMuchForPallet_RejectsWithoutChanges()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            SeedData seed;

            await using (var setup = new WarehouseDbContext(options))
            {
                seed = await SeedAsync(
                    setup,
                    orderedQuantity: 30m,
                    availableQuantity: 20m);
            }

            await using (var context = new WarehouseDbContext(options))
            {
                await Assert.ThrowsAsync<PickingConflictException>(
                    () => CreateService(context).CreateAsync(
                        seed.OrderId,
                        Request(seed, 21m),
                        Cancellation));
            }

            await AssertUnchangedAsync(options, seed, 20m);
        });
    }

    // ---------- 7. Batch mismatch ----------

    [Fact]
    public async Task Create_WrongBatch_RejectsWithoutChanges()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            SeedData seed;

            await using (var setup = new WarehouseDbContext(options))
            {
                seed = await SeedAsync(
                    setup,
                    differentOrderBatch: true);
            }

            await using (var context = new WarehouseDbContext(options))
            {
                await Assert.ThrowsAsync<InvalidPickingOperationException>(
                    () => CreateService(context).CreateAsync(
                        seed.OrderId,
                        Request(seed, 2m),
                        Cancellation));
            }

            await AssertUnchangedAsync(options, seed, 20m);
        });
    }

    // ---------- 8. Inactive location ----------

    [Fact]
    public async Task Create_InactiveLocation_RejectsWithoutChanges()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            SeedData seed;

            await using (var setup = new WarehouseDbContext(options))
            {
                seed = await SeedAsync(setup);

                var location = await setup.StorageLocations
                    .SingleAsync(x => x.Id == seed.LocationId);

                location.IsActive = false;

                await setup.SaveChangesAsync(Cancellation);
            }

            await using (var context = new WarehouseDbContext(options))
            {
                await Assert.ThrowsAsync<InvalidPickingOperationException>(
                    () => CreateService(context).CreateAsync(
                        seed.OrderId,
                        Request(seed, 2m),
                        Cancellation));
            }

            await AssertUnchangedAsync(options, seed, 20m);
        });
    }

    // ---------- 9. SQL Server concurrency and rollback ----------

    [Fact]
    public async Task Save_StaleRowVersion_RejectsAndRollsBackOperation()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            SeedData seed;

            await using (var setup = new WarehouseDbContext(options))
            {
                seed = await SeedAsync(setup);
            }

            await using var contextA =
                new WarehouseDbContext(options);

            await using var contextB =
                new WarehouseDbContext(options);

            var repositoryA = new PickingRepository(contextA);
            var repositoryB = new PickingRepository(contextB);

            // Both workers read the same original database state.

            var orderItemA = await repositoryA.GetTrackedOrderItemAsync(
                seed.OrderId, seed.OrderItemId, Cancellation);

            var palletItemA = await repositoryA.GetTrackedPalletItemAsync(
                seed.PalletItemId, Cancellation);

            var orderItemB = await repositoryB.GetTrackedOrderItemAsync(
                seed.OrderId, seed.OrderItemId, Cancellation);

            var palletItemB = await repositoryB.GetTrackedPalletItemAsync(
                seed.PalletItemId, Cancellation);

            Assert.NotNull(orderItemA);
            Assert.NotNull(palletItemA);
            Assert.NotNull(orderItemB);
            Assert.NotNull(palletItemB);

            // Worker A picks 3 pieces and saves successfully.

            palletItemA!.Quantity -= 3m;
            palletItemA.LogisticUnit.Revision++;
            orderItemA!.WarehouseOrder.Revision++;

            await repositoryA.AddAsync(
                NewOperation(seed, 3m),
                Cancellation);

            await repositoryA.SaveChangesAsync(Cancellation);

            // Worker B still has the old RowVersion.

            palletItemB!.Quantity -= 4m;
            palletItemB.LogisticUnit.Revision++;
            orderItemB!.WarehouseOrder.Revision++;

            await repositoryB.AddAsync(
                NewOperation(seed, 4m),
                Cancellation);

            await Assert.ThrowsAsync<
                ConcurrentPickingOperationException>(
                () => repositoryB.SaveChangesAsync(Cancellation));

            // The failed transaction must not leave a second
            // operation or incorrectly reduce stock.

            await using var verification =
                new WarehouseDbContext(options);

            var palletItem = await verification.LogisticUnitItems
                .AsNoTracking()
                .SingleAsync(x => x.Id == seed.PalletItemId);

            var order = await verification.WarehouseOrders
                .AsNoTracking()
                .SingleAsync(x => x.Id == seed.OrderId);

            var pallet = await verification.LogisticUnits
                .AsNoTracking()
                .SingleAsync(x => x.Id == seed.PalletId);

            Assert.Equal(17m, palletItem.Quantity);
            Assert.Equal(1, order.Revision);
            Assert.Equal(1, pallet.Revision);

            var history = await verification.PickingOperations
                .AsNoTracking()
                .ToListAsync();

            Assert.Single(history);
            Assert.Equal(3m, history[0].Quantity);
        });
    }

    // ---------- 10. History and picking progress ----------

    [Fact]
    public async Task GetPage_AndProgress_ReturnPersistedOperations()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            SeedData seed;

            await using (var setup = new WarehouseDbContext(options))
            {
                seed = await SeedAsync(setup);
            }

            foreach (var quantity in new[] { 1m, 2m, 3m })
            {
                await using var context =
                    new WarehouseDbContext(options);

                var result = await CreateService(context).CreateAsync(
                    seed.OrderId,
                    Request(seed, quantity),
                    Cancellation);

                Assert.NotNull(result);
            }

            await using var verification =
                new WarehouseDbContext(options);

            var pickingRepository =
                new PickingRepository(verification);

            var orderRepository =
                new WarehouseOrderRepository(verification);

            var firstPage = await pickingRepository.GetPageAsync(
                seed.OrderId,
                new PickingListQuery { Page = 1, PageSize = 2 },
                Cancellation);

            Assert.Equal(3, firstPage.TotalCount);
            Assert.Equal(2, firstPage.Items.Count);

            var secondPage = await pickingRepository.GetPageAsync(
                seed.OrderId,
                new PickingListQuery { Page = 2, PageSize = 2 },
                Cancellation);

            Assert.Equal(3, secondPage.TotalCount);
            Assert.Single(secondPage.Items);

            var firstOperation = firstPage.Items[0];

            var byId = await pickingRepository.GetByIdAsync(
                seed.OrderId,
                firstOperation.Id,
                Cancellation);

            Assert.NotNull(byId);
            Assert.Equal(firstOperation.RequestId, byId!.RequestId);
            Assert.Equal(seed.PalletItemId, byId.LogisticUnitItemId);

            var totalPicked = await pickingRepository.GetPickedQuantityAsync(
                seed.OrderItemId, Cancellation);

            Assert.Equal(6m, totalPicked);

            var progress = await orderRepository.GetItemProgressAsync(
                seed.OrderId, Cancellation);

            var line = Assert.Single(progress);

            Assert.Equal(10m, line.QuantityRequested);
            Assert.Equal(6m, line.QuantityPicked);

            var remainingStock = await verification.LogisticUnitItems
                .Where(x => x.Id == seed.PalletItemId)
                .Select(x => x.Quantity)
                .SingleAsync();

            Assert.Equal(14m, remainingStock);
        });
    }

    // ---------- Helpers ----------

    private sealed record SeedData(
        int OrderId,
        int OrderItemId,
        int PalletId,
        int PalletItemId,
        int LocationId);

    private static PickingService CreateService(
        WarehouseDbContext context)
    {
        return new PickingService(
            new PickingRepository(context),
            new WarehouseOrderRepository(context));
    }

    private static CreatePickingRequest Request(
        SeedData seed,
        decimal quantity,
        Guid? requestId = null)
    {
        return new CreatePickingRequest
        {
            RequestId = requestId ?? Guid.NewGuid(),
            WarehouseOrderItemId = seed.OrderItemId,
            LogisticUnitItemId = seed.PalletItemId,
            Quantity = quantity
        };
    }

    private static PickingOperation NewOperation(
        SeedData seed,
        decimal quantity)
    {
        return new PickingOperation
        {
            RequestId = Guid.NewGuid(),
            WarehouseOrderId = seed.OrderId,
            WarehouseOrderItemId = seed.OrderItemId,
            LogisticUnitId = seed.PalletId,
            LogisticUnitItemId = seed.PalletItemId,
            LogisticUnitCode = "LE-PICK-001",
            ProductCode = "ART-PICK-001",
            BatchNumber = "BATCH-001",
            SourceLocationCode = "A-01",
            Quantity = quantity,
            PickedAtUtc = DateTime.UtcNow
        };
    }

    private static async Task<SeedData> SeedAsync(
        WarehouseDbContext context,
        decimal orderedQuantity = 10m,
        decimal availableQuantity = 20m,
        bool differentOrderBatch = false)
    {
        var warehouse = new WarehouseEntity
        {
            Code = "WH-PICK-001",
            Name = "Picking Test Warehouse",
            IsActive = true
        };

        var zone = new WarehouseZone
        {
            Warehouse = warehouse,
            Code = "ZONE-A",
            Name = "Picking Zone",
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
            Code = "CAT-PICK",
            Name = "Picking Test Category",
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
            Code = "ART-PICK-001",
            Name = "Picking Test Product",
            ProductCategory = category,
            UnitOfMeasure = unitOfMeasure,
            IsActive = true
        };

        var batch = new ProductBatch
        {
            Product = product,
            BatchNumber = "BATCH-001",
            IsActive = true
        };

        var orderBatch = differentOrderBatch
            ? new ProductBatch
            {
                Product = product,
                BatchNumber = "BATCH-002",
                IsActive = true
            }
            : batch;

        var palletItem = new LogisticUnitItem
        {
            Product = product,
            ProductBatch = batch,
            Quantity = availableQuantity
        };

        var pallet = new LogisticUnit
        {
            Code = "LE-PICK-001",
            Status = LogisticUnitStatus.Stored,
            StorageLocation = location,
            CreatedAtUtc = DateTime.UtcNow,
            Items = new List<LogisticUnitItem>
            {
                palletItem
            }
        };

        var orderItem = new WarehouseOrderItem
        {
            Product = product,
            ProductBatch = orderBatch,
            QuantityRequested = orderedQuantity
        };

        var order = new WarehouseOrder
        {
            Number = "ORD-PICK-001",
            Warehouse = warehouse,
            Status = WarehouseOrderStatus.Released,
            CreatedAtUtc = DateTime.UtcNow,
            ReleasedAtUtc = DateTime.UtcNow,
            Items = new List<WarehouseOrderItem>
            {
                orderItem
            }
        };

        context.WarehouseOrders.Add(order);
        context.LogisticUnits.Add(pallet);

        await context.SaveChangesAsync(Cancellation);

        return new SeedData(
            order.Id,
            orderItem.Id,
            pallet.Id,
            palletItem.Id,
            location.Id);
    }

    private static async Task AssertUnchangedAsync(
        DbContextOptions<WarehouseDbContext> options,
        SeedData seed,
        decimal expectedStock)
    {
        await using var verification =
            new WarehouseDbContext(options);

        Assert.Equal(
            0,
            await verification.PickingOperations.CountAsync());

        var order = await verification.WarehouseOrders
            .AsNoTracking()
            .SingleAsync(x => x.Id == seed.OrderId);

        var pallet = await verification.LogisticUnits
            .AsNoTracking()
            .SingleAsync(x => x.Id == seed.PalletId);

        var item = await verification.LogisticUnitItems
            .AsNoTracking()
            .SingleAsync(x => x.Id == seed.PalletItemId);

        Assert.Equal(WarehouseOrderStatus.Released, order.Status);
        Assert.Null(order.CompletedAtUtc);
        Assert.Equal(0, order.Revision);
        Assert.Equal(0, pallet.Revision);
        Assert.Equal(expectedStock, item.Quantity);
    }

    private static async Task RunWithTemporaryDatabaseAsync(
        Func<DbContextOptions<WarehouseDbContext>, Task> test)
    {
        // Each test gets a completely separate random database.

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
                // Only delete the random database created above.

                await using var cleanup =
                    new WarehouseDbContext(options);

                await cleanup.Database.EnsureDeletedAsync();
            }
        }
    }
}

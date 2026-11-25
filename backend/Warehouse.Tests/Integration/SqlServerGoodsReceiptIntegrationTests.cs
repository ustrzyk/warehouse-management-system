
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.DTOs.GoodsReceipts;
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
public class SqlServerGoodsReceiptIntegrationTests
{
    private static readonly CancellationToken Cancellation =
        CancellationToken.None;

    // ---------- Creating goods receipts ----------

    [Fact]
    public async Task Create_ValidReceipt_PersistsDraftInSqlServer()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            int receiptId;

            await using (var context = new WarehouseDbContext(options))
            {
                var seed = await SeedAsync(context);

                var service = new GoodsReceiptService(
                    new GoodsReceiptRepository(context));

                var result = await service.CreateAsync(
                    new CreateGoodsReceiptRequest
                    {
                        Number = "  gr-it-001  ",
                        WarehouseId = seed.WarehouseId,
                        SupplierName = "  Example Supplier  "
                    },
                    Cancellation);

                receiptId = result.Id;

                Assert.True(receiptId > 0);
                Assert.Equal("GR-IT-001", result.Number);
                Assert.Equal("Draft", result.Status);
                Assert.Equal("Example Supplier", result.SupplierName);
                Assert.Null(result.LogisticUnitId);
            }

            // Read using a new DbContext.

            await using var verification =
                new WarehouseDbContext(options);

            var repository = new GoodsReceiptRepository(verification);

            var saved = await repository.GetByIdAsync(
                receiptId, Cancellation);

            Assert.NotNull(saved);
            Assert.Equal("GR-IT-001", saved!.Number);
            Assert.Equal(GoodsReceiptStatus.Draft, saved.Status);
            Assert.Equal("Example Supplier", saved.SupplierName);
            Assert.Null(saved.ReceivedAtUtc);
            Assert.Null(saved.LogisticUnitId);
            Assert.NotEmpty(saved.RowVersion);
            Assert.Equal("WH-IT-001", saved.Warehouse.Code);

            Assert.True(await repository.NumberExistsAsync(
                "GR-IT-001", Cancellation));
        });
    }

    // ---------- Adding products to a receipt ----------

    [Fact]
    public async Task AddItem_WithBatch_PersistsQuantityAndBatch()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            int receiptId;
            int itemId;

            await using (var context = new WarehouseDbContext(options))
            {
                var seed = await SeedAsync(context);

                receiptId = await CreateReceiptAsync(
                    context, seed.WarehouseId);

                var service = new GoodsReceiptService(
                    new GoodsReceiptRepository(context));

                var result = await service.AddItemAsync(
                    receiptId,
                    new AddGoodsReceiptItemRequest
                    {
                        ProductId = seed.ProductId,
                        ProductBatchId = seed.BatchId,
                        Quantity = 12.345m
                    },
                    Cancellation);

                Assert.NotNull(result);

                itemId = result!.Id;

                Assert.True(itemId > 0);
                Assert.Equal("ART-IT-001", result.ProductCode);
                Assert.Equal("BATCH-001", result.BatchNumber);
                Assert.Equal(12.345m, result.Quantity);
            }

            await using var verification =
                new WarehouseDbContext(options);

            var repository = new GoodsReceiptRepository(verification);

            var saved = await repository.GetItemByIdAsync(
                receiptId, itemId, Cancellation);

            Assert.NotNull(saved);
            Assert.Equal(12.345m, saved!.Quantity);
            Assert.Equal("ART-IT-001", saved.Product.Code);
            Assert.Equal("BATCH-001", saved.ProductBatch!.BatchNumber);

            var receipt = await repository.GetByIdAsync(
                receiptId, Cancellation);

            Assert.NotNull(receipt);
            Assert.Equal(GoodsReceiptStatus.Draft, receipt!.Status);
            Assert.Equal(1, receipt.Revision);
        });
    }

    // ---------- Confirming a receipt ----------

    [Fact]
    public async Task Confirm_ValidReceipt_CreatesPalletAndItemsAtomically()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            int receiptId;
            int palletId;

            await using (var context = new WarehouseDbContext(options))
            {
                var seed = await SeedAsync(context);

                receiptId = await CreateReceiptAsync(
                    context, seed.WarehouseId);

                await AddItemAsync(
                    context, receiptId,
                    seed.ProductId, seed.BatchId, 7.125m);

                await AddItemAsync(
                    context, receiptId,
                    seed.ProductId, null, 5m);

                var service = new GoodsReceiptService(
                    new GoodsReceiptRepository(context));

                var result = await service.ConfirmAsync(
                    receiptId, Cancellation);

                Assert.NotNull(result);
                Assert.Equal("Received", result!.Status);
                Assert.NotNull(result.ReceivedAtUtc);
                Assert.NotNull(result.LogisticUnitId);

                palletId = result.LogisticUnitId!.Value;
            }

            // Verify persisted state in a separate context.

            await using var verification =
                new WarehouseDbContext(options);

            var receipt = await verification.GoodsReceipts
                .AsNoTracking()
                .FirstAsync(x => x.Id == receiptId);

            var pallet = await verification.LogisticUnits
                .AsNoTracking()
                .Include(x => x.Items)
                .FirstAsync(x => x.Id == palletId);

            Assert.Equal(GoodsReceiptStatus.Received, receipt.Status);
            Assert.NotNull(receipt.ReceivedAtUtc);
            Assert.Equal(palletId, receipt.LogisticUnitId);
            Assert.Equal(3, receipt.Revision);

            Assert.Equal(
                $"LE-GR-{receiptId:D8}-AUTO",
                pallet.Code);

            Assert.Equal(LogisticUnitStatus.Created, pallet.Status);
            Assert.Null(pallet.StorageLocationId);

            Assert.Equal(2, pallet.Items.Count);

            var logisticUnitService = new LogisticUnitService(
                new LogisticUnitRepository(verification));
            var palletDto = await logisticUnitService.GetByIdAsync(
                palletId, Cancellation);
            Assert.NotNull(palletDto);
            Assert.True(palletDto!.IsGeneratedByGoodsReceipt);
            Assert.Equal(receipt.WarehouseId, palletDto.SourceWarehouseId);

            Assert.Contains(pallet.Items, item =>
                item.ProductBatchId.HasValue &&
                item.Quantity == 7.125m);

            Assert.Contains(pallet.Items, item =>
                item.ProductBatchId == null &&
                item.Quantity == 5m);
        });
    }

    // ---------- Unique receipt number ----------

    [Fact]
    public async Task Add_DuplicateReceiptNumber_ThrowsConflict()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            int warehouseId;

            await using (var context = new WarehouseDbContext(options))
            {
                var seed = await SeedAsync(context);
                warehouseId = seed.WarehouseId;

                await CreateReceiptAsync(
                    context, warehouseId, "GR-DUPLICATE");
            }

            await using var secondContext =
                new WarehouseDbContext(options);

            var repository = new GoodsReceiptRepository(secondContext);

            await repository.AddAsync(
                new GoodsReceipt
                {
                    Number = "GR-DUPLICATE",
                    WarehouseId = warehouseId,
                    Status = GoodsReceiptStatus.Draft,
                    CreatedAtUtc = DateTime.UtcNow
                },
                Cancellation);

            await Assert.ThrowsAsync<DuplicateGoodsReceiptException>(
                () => repository.SaveChangesAsync(Cancellation));
        });
    }

    // ---------- Duplicate receipt item ----------

    [Fact]
    public async Task AddItem_DuplicateProductAndBatch_ThrowsConflict()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            int receiptId;
            int productId;
            int batchId;

            await using (var context = new WarehouseDbContext(options))
            {
                var seed = await SeedAsync(context);

                productId = seed.ProductId;
                batchId = seed.BatchId;

                receiptId = await CreateReceiptAsync(
                    context, seed.WarehouseId);

                await AddItemAsync(
                    context,
                    receiptId,
                    productId,
                    batchId,
                    10m);
            }

            await using var secondContext =
                new WarehouseDbContext(options);

            var repository = new GoodsReceiptRepository(secondContext);

            await repository.AddItemAsync(
                new GoodsReceiptItem
                {
                    GoodsReceiptId = receiptId,
                    ProductId = productId,
                    ProductBatchId = batchId,
                    Quantity = 5m
                },
                Cancellation);

            await Assert.ThrowsAsync<DuplicateGoodsReceiptException>(
                () => repository.SaveChangesAsync(Cancellation));
        });
    }

    // ---------- SQL Server RowVersion ----------

    [Fact]
    public async Task Update_StaleRowVersion_ThrowsConcurrencyException()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            int receiptId;

            await using (var setup = new WarehouseDbContext(options))
            {
                var seed = await SeedAsync(setup);

                receiptId = await CreateReceiptAsync(
                    setup, seed.WarehouseId);
            }

            await using var contextA =
                new WarehouseDbContext(options);

            await using var contextB =
                new WarehouseDbContext(options);

            var repositoryA = new GoodsReceiptRepository(contextA);
            var repositoryB = new GoodsReceiptRepository(contextB);

            var receiptA = await repositoryA.GetTrackedByIdAsync(
                receiptId, Cancellation);

            var receiptB = await repositoryB.GetTrackedByIdAsync(
                receiptId, Cancellation);

            Assert.NotNull(receiptA);
            Assert.NotNull(receiptB);

            var originalVersion = receiptA!.RowVersion.ToArray();

            // First user updates and saves successfully.

            receiptA.SupplierName = "First user";
            receiptA.Revision++;

            await repositoryA.SaveChangesAsync(Cancellation);

            Assert.False(
                originalVersion.SequenceEqual(receiptA.RowVersion));

            // Second user tries to save outdated data.

            receiptB!.SupplierName = "Second user";
            receiptB.Revision++;

            await Assert.ThrowsAsync<
                ConcurrentGoodsReceiptModificationException>(
                () => repositoryB.SaveChangesAsync(Cancellation));

            await using var verification =
                new WarehouseDbContext(options);

            var actual = await verification.GoodsReceipts
                .AsNoTracking()
                .FirstAsync(x => x.Id == receiptId);

            Assert.Equal("First user", actual.SupplierName);
            Assert.Equal(1, actual.Revision);
        });
    }

    // ---------- Transaction rollback ----------

    [Fact]
    public async Task Confirm_PalletCodeConflict_DoesNotPartiallyConfirmReceipt()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            int receiptId;
            string conflictingPalletCode;

            await using (var setup = new WarehouseDbContext(options))
            {
                var seed = await SeedAsync(setup);

                receiptId = await CreateReceiptAsync(
                    setup, seed.WarehouseId);

                await AddItemAsync(
                    setup,
                    receiptId,
                    seed.ProductId,
                    seed.BatchId,
                    8m);

                conflictingPalletCode =
                    $"LE-GR-{receiptId:D8}-AUTO";

                // Occupy the code that ConfirmAsync will try to use.

                setup.LogisticUnits.Add(new LogisticUnit
                {
                    Code = conflictingPalletCode,
                    Status = LogisticUnitStatus.Created,
                    CreatedAtUtc = DateTime.UtcNow
                });

                await setup.SaveChangesAsync(Cancellation);
            }

            await using (var confirmationContext =
                new WarehouseDbContext(options))
            {
                var service = new GoodsReceiptService(
                    new GoodsReceiptRepository(confirmationContext));

                await Assert.ThrowsAsync<DuplicateGoodsReceiptException>(
                    () => service.ConfirmAsync(
                        receiptId, Cancellation));
            }

            // Verify that the failed SaveChanges did not
            // persist the receipt's new status or pallet link.

            await using var verification =
                new WarehouseDbContext(options);

            var receipt = await verification.GoodsReceipts
                .AsNoTracking()
                .FirstAsync(x => x.Id == receiptId);

            Assert.Equal(GoodsReceiptStatus.Draft, receipt.Status);
            Assert.Null(receipt.ReceivedAtUtc);
            Assert.Null(receipt.LogisticUnitId);
            Assert.Equal(1, receipt.Revision);

            Assert.Equal(
                1,
                await verification.LogisticUnits.CountAsync(
                    x => x.Code == conflictingPalletCode));

            Assert.Equal(
                0,
                await verification.LogisticUnitItems.CountAsync());
        });
    }

    // ---------- SQL filtering and pagination ----------

    [Fact]
    public async Task GetPage_FiltersBySupplierAndPaginates()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            int warehouseId;

            await using (var setup = new WarehouseDbContext(options))
            {
                var seed = await SeedAsync(setup);
                warehouseId = seed.WarehouseId;

                var time = DateTime.UtcNow;

                setup.GoodsReceipts.AddRange(
                    NewReceipt(
                        warehouseId,
                        "GR-IT-A",
                        "Target Supplier",
                        time.AddMinutes(-3)),
                    NewReceipt(
                        warehouseId,
                        "GR-IT-B",
                        "Target Supplier",
                        time.AddMinutes(-2)),
                    NewReceipt(
                        warehouseId,
                        "GR-IT-C",
                        "Other Supplier",
                        time.AddMinutes(-1))
                );

                await setup.SaveChangesAsync(Cancellation);
            }

            await using var queryContext =
                new WarehouseDbContext(options);

            var repository = new GoodsReceiptRepository(queryContext);

            var firstPage = await repository.GetPageAsync(
                new GoodsReceiptListQuery
                {
                    WarehouseId = warehouseId,
                    Search = "Target",
                    Status = GoodsReceiptStatus.Draft,
                    Page = 1,
                    PageSize = 1
                },
                Cancellation);

            Assert.Equal(2, firstPage.TotalCount);
            Assert.Single(firstPage.Items);
            Assert.Equal("GR-IT-B", firstPage.Items[0].Number);

            var secondPage = await repository.GetPageAsync(
                new GoodsReceiptListQuery
                {
                    WarehouseId = warehouseId,
                    Search = "Target",
                    Status = GoodsReceiptStatus.Draft,
                    Page = 2,
                    PageSize = 1
                },
                Cancellation);

            Assert.Equal(2, secondPage.TotalCount);
            Assert.Single(secondPage.Items);
            Assert.Equal("GR-IT-A", secondPage.Items[0].Number);
        });
    }

    // ---------- Test helpers ----------

    private sealed record SeedData(
        int WarehouseId,
        int ProductId,
        int BatchId);

    private static async Task<SeedData> SeedAsync(
        WarehouseDbContext context)
    {
        var warehouse = new WarehouseEntity
        {
            Code = "WH-IT-001",
            Name = "Integration Test Warehouse",
            IsActive = true
        };

        var category = new ProductCategory
        {
            Code = "CAT-IT",
            Name = "Integration Category",
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
            Code = "ART-IT-001",
            Name = "Integration Test Product",
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

        context.Warehouses.Add(warehouse);
        context.Products.Add(product);
        context.ProductBatches.Add(batch);

        await context.SaveChangesAsync(Cancellation);

        return new SeedData(
            warehouse.Id,
            product.Id,
            batch.Id);
    }

    private static async Task<int> CreateReceiptAsync(
        WarehouseDbContext context,
        int warehouseId,
        string number = "GR-IT-001")
    {
        var service = new GoodsReceiptService(
            new GoodsReceiptRepository(context));

        var result = await service.CreateAsync(
            new CreateGoodsReceiptRequest
            {
                Number = number,
                WarehouseId = warehouseId,
                SupplierName = "Integration Supplier"
            },
            Cancellation);

        return result.Id;
    }

    private static async Task AddItemAsync(
        WarehouseDbContext context,
        int receiptId,
        int productId,
        int? batchId,
        decimal quantity)
    {
        var service = new GoodsReceiptService(
            new GoodsReceiptRepository(context));

        var result = await service.AddItemAsync(
            receiptId,
            new AddGoodsReceiptItemRequest
            {
                ProductId = productId,
                ProductBatchId = batchId,
                Quantity = quantity
            },
            Cancellation);

        Assert.NotNull(result);
        Assert.Equal(quantity, result!.Quantity);
    }

    private static GoodsReceipt NewReceipt(
        int warehouseId,
        string number,
        string supplier,
        DateTime createdAt)
    {
        return new GoodsReceipt
        {
            Number = number,
            WarehouseId = warehouseId,
            SupplierName = supplier,
            Status = GoodsReceiptStatus.Draft,
            CreatedAtUtc = createdAt
        };
    }

    private static async Task RunWithTemporaryDatabaseAsync(
        Func<DbContextOptions<WarehouseDbContext>, Task> test)
    {
        // Each test creates its own uniquely named database.

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
                // Delete ONLY this test's temporary database.

                await using var cleanup =
                    new WarehouseDbContext(options);

                await cleanup.Database.EnsureDeletedAsync();
            }
        }
    }
}

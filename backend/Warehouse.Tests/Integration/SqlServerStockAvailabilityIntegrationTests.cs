
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.DTOs.Stock;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;
using Warehouse.Infrastructure.Persistence;
using Warehouse.Infrastructure.Repositories;
using WarehouseEntity = Warehouse.Domain.Entities.Warehouse;

namespace Warehouse.Tests.Integration;

[Trait("Category", "SqlServer")]
public class SqlServerStockAvailabilityIntegrationTests
{
    [Fact]
    public async Task GetPage_CalculatesPhysicalOutstandingAndFreeStock()
    {
        await WithDatabaseAsync(async options =>
        {
            SeedIds ids;

            await using (var setup = new WarehouseDbContext(options))
            {
                ids = await SeedAsync(setup);
            }

            await using var db = new WarehouseDbContext(options);
            var repository = new StockAvailabilityRepository(db);

            var (agroRows, agroCount) =
                await repository.GetPageAsync(
                    new StockAvailabilityQuery
                    {
                        WarehouseId = ids.AgroId,
                        Search = "AGR-NPK-25"
                    },
                    CancellationToken.None);

            Assert.Equal(1, agroCount);

            var agro = Assert.Single(agroRows);

            Assert.Equal("WH-AGRO", agro.WarehouseCode);
            Assert.Equal("AGR-NPK-25", agro.ProductCode);
            Assert.Equal(190m, agro.PhysicalQuantity);
            Assert.Equal(30m, agro.OutstandingQuantity);
            Assert.Equal(160m, agro.FreeQuantity);

            // Zamówienia Draft, Completed i Shipped
            // nie powiększają zapotrzebowania.
            var (homeRows, homeCount) =
                await repository.GetPageAsync(
                    new StockAvailabilityQuery
                    {
                        WarehouseId = ids.HomeId
                    },
                    CancellationToken.None);

            Assert.Equal(1, homeCount);

            var home = Assert.Single(homeRows);

            Assert.Equal("DOM-LAUN-C3", home.ProductCode);
            Assert.Equal(185m, home.PhysicalQuantity);
            Assert.Equal(0m, home.OutstandingQuantity);
            Assert.Equal(185m, home.FreeQuantity);

            // Paginacja odbywa się po agregacji.
            var (page, totalCount) =
                await repository.GetPageAsync(
                    new StockAvailabilityQuery
                    {
                        Page = 1,
                        PageSize = 1
                    },
                    CancellationToken.None);

            Assert.Equal(2, totalCount);
            Assert.Single(page);
        });
    }

    [Fact]
    public async Task GetPage_PartialPickingDoesNotDoubleSubtractStock()
    {
        await WithDatabaseAsync(async options =>
        {
            SeedIds ids;

            await using (var setup = new WarehouseDbContext(options))
            {
                ids = await SeedAsync(setup);
            }

            // Pierwsze pobranie: 10 sztuk.
            await using (var picking = new WarehouseDbContext(options))
            {
                var palletItem = await picking.LogisticUnitItems
                    .SingleAsync(x => x.Id == ids.PalletItemId);

                palletItem.Quantity -= 10m;

                picking.PickingOperations.Add(
                    new PickingOperation
                    {
                        RequestId = Guid.NewGuid(),
                        WarehouseOrderId = ids.ReleasedOrderId,
                        WarehouseOrderItemId = ids.ReleasedItemId,
                        LogisticUnitId = palletItem.LogisticUnitId,
                        LogisticUnitItemId = palletItem.Id,
                        LogisticUnitCode = "LE-AG-0001",
                        ProductCode = "AGR-NPK-25",
                        BatchNumber = "B-2026-NPK",
                        SourceLocationCode = "AD-01",
                        Quantity = 10m
                    });

                await picking.SaveChangesAsync();
            }

            await using (var verification =
                new WarehouseDbContext(options))
            {
                var repository =
                    new StockAvailabilityRepository(verification);

                var (rows, _) = await repository.GetPageAsync(
                    new StockAvailabilityQuery
                    {
                        Search = "AGR-NPK-25"
                    },
                    CancellationToken.None);

                var item = Assert.Single(rows);

                Assert.Equal(180m, item.PhysicalQuantity);
                Assert.Equal(20m, item.OutstandingQuantity);
                Assert.Equal(160m, item.FreeQuantity);
            }

            // Pobranie pozostałych 20 sztuk.
            await using (var picking =
                new WarehouseDbContext(options))
            {
                var palletItem = await picking.LogisticUnitItems
                    .SingleAsync(x => x.Id == ids.PalletItemId);

                var order = await picking.WarehouseOrders
                    .SingleAsync(x => x.Id == ids.ReleasedOrderId);

                palletItem.Quantity -= 20m;

                picking.PickingOperations.Add(
                    new PickingOperation
                    {
                        RequestId = Guid.NewGuid(),
                        WarehouseOrderId = ids.ReleasedOrderId,
                        WarehouseOrderItemId = ids.ReleasedItemId,
                        LogisticUnitId = palletItem.LogisticUnitId,
                        LogisticUnitItemId = palletItem.Id,
                        LogisticUnitCode = "LE-AG-0001",
                        ProductCode = "AGR-NPK-25",
                        BatchNumber = "B-2026-NPK",
                        SourceLocationCode = "AD-01",
                        Quantity = 20m
                    });

                order.Status = WarehouseOrderStatus.Completed;
                order.CompletedAtUtc = DateTime.UtcNow;
                order.Revision++;

                await picking.SaveChangesAsync();
            }

            await using var finalDb =
                new WarehouseDbContext(options);

            var finalRepository =
                new StockAvailabilityRepository(finalDb);

            var (finalRows, finalCount) =
                await finalRepository.GetPageAsync(
                    new StockAvailabilityQuery
                    {
                        Search = "AGR-NPK-25"
                    },
                    CancellationToken.None);

            Assert.Equal(1, finalCount);

            var result = Assert.Single(finalRows);

            Assert.Equal(160m, result.PhysicalQuantity);
            Assert.Equal(0m, result.OutstandingQuantity);
            Assert.Equal(160m, result.FreeQuantity);
        });
    }

    private record SeedIds(
        int AgroId,
        int HomeId,
        int PalletItemId,
        int ReleasedOrderId,
        int ReleasedItemId);

    private static async Task<SeedIds> SeedAsync(
        WarehouseDbContext db)
    {
        var agro = new WarehouseEntity
        {
            Code = "WH-AGRO",
            Name = "Agricultural Warehouse",
            IsActive = true
        };

        var home = new WarehouseEntity
        {
            Code = "WH-HOME",
            Name = "Household Warehouse",
            IsActive = true
        };

        var agroZone = new WarehouseZone
        {
            Code = "AG-DRY",
            Name = "Dry products",
            Warehouse = agro,
            IsActive = true
        };

        var homeZone = new WarehouseZone
        {
            Code = "HM-LAUN",
            Name = "Laundry products",
            Warehouse = home,
            IsActive = true
        };

        var ad01 = new StorageLocation
        {
            Code = "AD-01",
            WarehouseZone = agroZone,
            IsActive = true
        };

        var ad02 = new StorageLocation
        {
            Code = "AD-02",
            WarehouseZone = agroZone,
            IsActive = true
        };

        var hl01 = new StorageLocation
        {
            Code = "HL-01",
            WarehouseZone = homeZone,
            IsActive = true
        };

        var category = new ProductCategory
        {
            Code = "TEST-CAT",
            Name = "Test Category",
            IsActive = true
        };

        var unit = new UnitOfMeasure
        {
            Code = "PCS",
            Name = "Pieces",
            Symbol = "pcs",
            IsActive = true
        };

        var npk = new Product
        {
            Code = "AGR-NPK-25",
            Name = "Nawoz NPK",
            ProductCategory = category,
            UnitOfMeasure = unit,
            IsActive = true
        };

        var laundry = new Product
        {
            Code = "DOM-LAUN-C3",
            Name = "Zel do prania",
            ProductCategory = category,
            UnitOfMeasure = unit,
            IsActive = true
        };

        var batch = new ProductBatch
        {
            Product = npk,
            BatchNumber = "B-2026-NPK",
            IsActive = true
        };

        var palletItem = new LogisticUnitItem
        {
            Product = npk,
            ProductBatch = batch,
            Quantity = 125m
        };

        db.LogisticUnits.AddRange(
            new LogisticUnit
            {
                Code = "LE-AG-0001",
                Status = LogisticUnitStatus.Stored,
                StorageLocation = ad01,
                Items = new List<LogisticUnitItem>
                {
                    palletItem
                }
            },
            new LogisticUnit
            {
                Code = "LE-AG-0003",
                Status = LogisticUnitStatus.Stored,
                StorageLocation = ad02,
                Items = new List<LogisticUnitItem>
                {
                    new()
                    {
                        Product = npk,
                        ProductBatch = batch,
                        Quantity = 65m
                    }
                }
            },
            new LogisticUnit
            {
                Code = "LE-HM-0001",
                Status = LogisticUnitStatus.Stored,
                StorageLocation = hl01,
                Items = new List<LogisticUnitItem>
                {
                    new()
                    {
                        Product = laundry,
                        Quantity = 185m
                    }
                }
            }
        );

        WarehouseOrder CreateOrder(
            string number,
            WarehouseOrderStatus status,
            decimal quantity)
        {
            return new WarehouseOrder
            {
                Number = number,
                Warehouse = agro,
                Status = status,
                Items = new List<WarehouseOrderItem>
                {
                    new()
                    {
                        Product = npk,
                        QuantityRequested = quantity
                    }
                }
            };
        }

        var released = CreateOrder(
            "ORD-AG-REL",
            WarehouseOrderStatus.Released,
            30m);

        released.ReleasedAtUtc = DateTime.UtcNow;

        db.WarehouseOrders.AddRange(
            released,
            CreateOrder(
                "ORD-AG-DRAFT",
                WarehouseOrderStatus.Draft,
                50m),
            CreateOrder(
                "ORD-AG-COMP",
                WarehouseOrderStatus.Completed,
                40m),
            CreateOrder(
                "ORD-AG-SHIP",
                WarehouseOrderStatus.Shipped,
                20m)
        );

        await db.SaveChangesAsync();

        return new SeedIds(
            agro.Id,
            home.Id,
            palletItem.Id,
            released.Id,
            released.Items.Single().Id);
    }

    private static async Task WithDatabaseAsync(
        Func<DbContextOptions<WarehouseDbContext>, Task> test)
    {
        var databaseName =
            "WarehouseAvailabilityTest_" +
            Guid.NewGuid().ToString("N");

        var connectionString = new SqlConnectionStringBuilder
        {
            DataSource = @"(localdb)\MSSQLLocalDB",
            InitialCatalog = databaseName,
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            ConnectTimeout = 30
        }.ConnectionString;

        var options =
            new DbContextOptionsBuilder<WarehouseDbContext>()
                .UseSqlServer(connectionString)
                .Options;

        var created = false;

        try
        {
            await using (var setup =
                new WarehouseDbContext(options))
            {
                await setup.Database.MigrateAsync();
                created = true;
            }

            await test(options);
        }
        finally
        {
            if (created)
            {
                await using var cleanup =
                    new WarehouseDbContext(options);

                await cleanup.Database.EnsureDeletedAsync();
            }
        }
    }
}

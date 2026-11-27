
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
public class SqlServerStockPositionsIntegrationTests
{
    [Fact]
    public async Task GetPage_FiltersByWarehouseBatchAndLocation()
    {
        await WithDatabaseAsync(async options =>
        {
            int agroId;
            int homeId;

            await using (var setup = new WarehouseDbContext(options))
            {
                (agroId, homeId) = await SeedAsync(setup);
            }

            await using var context = new WarehouseDbContext(options);
            var repository = new StockPositionsRepository(context);

            var query = new StockPositionQuery
            {
                WarehouseId = agroId,
                Search = "AGR-NPK-25",
                BatchNumber = "B-2026-NPK",
                Page = 1,
                PageSize = 1
            };

            var (firstPage, totalCount) =
                await repository.GetPageAsync(
                    query, CancellationToken.None);

            Assert.Equal(2, totalCount);
            Assert.Single(firstPage);
            Assert.Equal("LE-AG-0001",
                firstPage[0].LogisticUnitCode);
            Assert.Equal(125m, firstPage[0].Quantity);
            Assert.Equal("WH-AGRO", firstPage[0].WarehouseCode);
            Assert.Equal(new DateOnly(2028, 10, 1),
                firstPage[0].ExpirationDate);

            query.Page = 2;

            var (secondPage, secondTotal) =
                await repository.GetPageAsync(
                    query, CancellationToken.None);

            Assert.Equal(2, secondTotal);
            Assert.Single(secondPage);
            Assert.Equal("LE-AG-0003",
                secondPage[0].LogisticUnitCode);
            Assert.Equal(65m, secondPage[0].Quantity);

            query.Page = 1;
            query.PageSize = 20;
            query.LocationCode = "AD-01";

            var (locationRows, locationCount) =
                await repository.GetPageAsync(
                    query, CancellationToken.None);

            Assert.Equal(1, locationCount);
            Assert.Single(locationRows);
            Assert.Equal("AD-01", locationRows[0].LocationCode);

            var (homeRows, homeCount) =
                await repository.GetPageAsync(
                    new StockPositionQuery
                    {
                        WarehouseId = homeId
                    },
                    CancellationToken.None);

            Assert.Equal(1, homeCount);
            Assert.Single(homeRows);
            Assert.Equal("DOM-LAUN-C3", homeRows[0].ProductCode);
        });
    }

    [Fact]
    public async Task GetPage_ExcludesUnavailableStockAndReadsCurrentQuantity()
    {
        await WithDatabaseAsync(async options =>
        {
            await using (var setup = new WarehouseDbContext(options))
            {
                await SeedAsync(setup);
            }

            await using (var context = new WarehouseDbContext(options))
            {
                var repository = new StockPositionsRepository(context);

                var (rows, count) =
                    await repository.GetPageAsync(
                        new StockPositionQuery(),
                        CancellationToken.None);

                Assert.Equal(4, count);
                Assert.Equal(4, rows.Count);

                Assert.DoesNotContain(rows,
                    x => x.LogisticUnitCode == "LE-AG-CREATED");

                Assert.DoesNotContain(rows,
                    x => x.LogisticUnitCode == "LE-AG-INACTIVE");

                Assert.DoesNotContain(rows,
                    x => x.LogisticUnitCode == "LE-AG-ZERO");
            }

            // Symulacja zmniejszenia stanu produktu.
            await using (var update = new WarehouseDbContext(options))
            {
                var item = await update.LogisticUnitItems
                    .SingleAsync(x =>
                        x.LogisticUnit.Code == "LE-AG-0001" &&
                        x.Product.Code == "AGR-NPK-25");

                item.Quantity -= 25m;
                await update.SaveChangesAsync();
            }

            // Nowy DbContext odczytuje aktualny stan z SQL Server.
            await using var verification =
                new WarehouseDbContext(options);

            var stockRepository =
                new StockPositionsRepository(verification);

            var (updatedRows, updatedCount) =
                await stockRepository.GetPageAsync(
                    new StockPositionQuery
                    {
                        Search = "AGR-NPK-25"
                    },
                    CancellationToken.None);

            Assert.Equal(2, updatedCount);

            var updated = Assert.Single(updatedRows,
                x => x.LogisticUnitCode == "LE-AG-0001");

            Assert.Equal(100m, updated.Quantity);
        });
    }

    private static async Task<(int AgroId, int HomeId)> SeedAsync(
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

        var a1 = new StorageLocation
        {
            Code = "AD-01",
            WarehouseZone = agroZone,
            IsActive = true
        };

        var a2 = new StorageLocation
        {
            Code = "AD-02",
            WarehouseZone = agroZone,
            IsActive = true
        };

        var inactive = new StorageLocation
        {
            Code = "AD-99",
            WarehouseZone = agroZone,
            IsActive = false
        };

        var homeLocation = new StorageLocation
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

        Product Product(string code) => new()
        {
            Code = code,
            Name = code + " test product",
            ProductCategory = category,
            UnitOfMeasure = unit,
            IsActive = true
        };

        var npk = Product("AGR-NPK-25");
        var fol = Product("AGR-FOL-05");
        var laundry = Product("DOM-LAUN-C3");

        var npkBatch = new ProductBatch
        {
            Product = npk,
            BatchNumber = "B-2026-NPK",
            ExpirationDate = new DateOnly(2028, 10, 1),
            IsActive = true
        };

        var folBatch = new ProductBatch
        {
            Product = fol,
            BatchNumber = "B-2026-FOL",
            IsActive = true
        };

        var laundryBatch = new ProductBatch
        {
            Product = laundry,
            BatchNumber = "B-2026-LAUN",
            IsActive = true
        };

        LogisticUnitItem Item(
            Product product,
            ProductBatch batch,
            decimal quantity) => new()
            {
                Product = product,
                ProductBatch = batch,
                Quantity = quantity
            };

        LogisticUnit Pallet(
            string code,
            LogisticUnitStatus status,
            StorageLocation? location,
            params LogisticUnitItem[] items) => new()
            {
                Code = code,
                Status = status,
                StorageLocation = location,
                Items = items.ToList()
            };

        db.LogisticUnits.AddRange(
            Pallet(
                "LE-AG-0001",
                LogisticUnitStatus.Stored,
                a1,
                Item(npk, npkBatch, 125m),
                Item(fol, folBatch, 80m)
            ),
            Pallet(
                "LE-AG-0003",
                LogisticUnitStatus.Stored,
                a2,
                Item(npk, npkBatch, 65m)
            ),
            Pallet(
                "LE-HM-0001",
                LogisticUnitStatus.Stored,
                homeLocation,
                Item(laundry, laundryBatch, 90m)
            ),
            Pallet(
                "LE-AG-CREATED",
                LogisticUnitStatus.Created,
                null,
                Item(npk, npkBatch, 40m)
            ),
            Pallet(
                "LE-AG-INACTIVE",
                LogisticUnitStatus.Stored,
                inactive,
                Item(npk, npkBatch, 40m)
            ),
            Pallet(
                "LE-AG-ZERO",
                LogisticUnitStatus.Stored,
                a2,
                Item(npk, npkBatch, 0m)
            )
        );

        await db.SaveChangesAsync();

        return (agro.Id, home.Id);
    }

    private static async Task WithDatabaseAsync(
        Func<DbContextOptions<WarehouseDbContext>, Task> test)
    {
        var databaseName =
            "WarehouseStockTest_" + Guid.NewGuid().ToString("N");

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

        var created = false;

        try
        {
            await using (var setup = new WarehouseDbContext(options))
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

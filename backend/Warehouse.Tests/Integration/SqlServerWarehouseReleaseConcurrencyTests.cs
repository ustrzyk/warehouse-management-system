using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Services;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;
using Warehouse.Infrastructure.Persistence;
using Warehouse.Infrastructure.Repositories;
using WarehouseEntity = Warehouse.Domain.Entities.Warehouse;

namespace Warehouse.Tests.Integration;

[Trait("Category", "SqlServer")]
public class SqlServerWarehouseReleaseConcurrencyTests
{
    [Fact]
    public async Task TwoOrdersOf80_WithStock100_OnlyOneIsReleased()
    {
        await WithDatabaseAsync(async options =>
        {
            int firstId;
            int secondId;

            await using (var setup = new WarehouseDbContext(options))
            {
                var warehouse = new WarehouseEntity
                {
                    Code = "WH-CONC",
                    Name = "Concurrency Warehouse",
                    IsActive = true
                };

                var location = new StorageLocation
                {
                    Code = "A-01",
                    IsActive = true,
                    WarehouseZone = new WarehouseZone
                    {
                        Code = "ZONE-A",
                        Name = "Zone A",
                        IsActive = true,
                        Warehouse = warehouse
                    }
                };

                var product = new Product
                {
                    Code = "ART-CONC",
                    Name = "Test Product",
                    IsActive = true,
                    ProductCategory = new ProductCategory
                    {
                        Code = "CAT-CONC",
                        Name = "Test Category",
                        IsActive = true
                    },
                    UnitOfMeasure = new UnitOfMeasure
                    {
                        Code = "PCS-CONC",
                        Name = "Pieces",
                        Symbol = "pcs",
                        IsActive = true
                    }
                };

                var pallet = new LogisticUnit
                {
                    Code = "LE-CONC",
                    Status = LogisticUnitStatus.Stored,
                    StorageLocation = location,
                    Items =
                    [
                        new LogisticUnitItem
                        {
                            Product = product,
                            Quantity = 100m
                        }
                    ]
                };

                WarehouseOrder NewOrder(string number) => new()
                {
                    Number = number,
                    Warehouse = warehouse,
                    Status = WarehouseOrderStatus.Draft,
                    Items =
                    [
                        new WarehouseOrderItem
                        {
                            Product = product,
                            QuantityRequested = 80m
                        }
                    ]
                };

                var first = NewOrder("ORD-CONC-A");
                var second = NewOrder("ORD-CONC-B");

                setup.LogisticUnits.Add(pallet);
                setup.WarehouseOrders.AddRange(first, second);

                await setup.SaveChangesAsync();

                firstId = first.Id;
                secondId = second.Id;
            }

            async Task<bool> TryReleaseAsync(int id)
            {
                await using var context =
                    new WarehouseDbContext(options);

                var service = new WarehouseOrderService(
                    new WarehouseOrderRepository(context));

                try
                {
                    var result = await service.ReleaseAsync(
                        id, CancellationToken.None);

                    return result?.Status == "Released";
                }
                catch (InsufficientWarehouseStockException)
                {
                    return false;
                }
            }

            var results = await Task.WhenAll(
                TryReleaseAsync(firstId),
                TryReleaseAsync(secondId));

            Assert.Single(results, x => x);

            await using var verification =
                new WarehouseDbContext(options);

            var statuses = await verification.WarehouseOrders
                .AsNoTracking()
                .Where(x => x.Id == firstId || x.Id == secondId)
                .Select(x => x.Status)
                .ToListAsync();

            Assert.Equal(1, statuses.Count(x =>
                x == WarehouseOrderStatus.Released));

            Assert.Equal(1, statuses.Count(x =>
                x == WarehouseOrderStatus.Draft));
        });
    }

    [Fact]
    public async Task Lock_SecondOperationWaitsForFirst()
    {
        await WithDatabaseAsync(async options =>
        {
            await using var contextA =
                new WarehouseDbContext(options);

            await using var contextB =
                new WarehouseDbContext(options);

            var repositoryA = new WarehouseOrderRepository(contextA);
            var repositoryB = new WarehouseOrderRepository(contextB);

            var firstEntered = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            var releaseFirst = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            var secondEntered = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            var firstTask =
                repositoryA.ExecuteWithWarehouseReleaseLockAsync(
                    1,
                    async ct =>
                    {
                        firstEntered.TrySetResult(true);
                        await releaseFirst.Task.WaitAsync(ct);
                        return true;
                    },
                    CancellationToken.None);

            await firstEntered.Task.WaitAsync(
                TimeSpan.FromSeconds(15));

            var secondTask =
                repositoryB.ExecuteWithWarehouseReleaseLockAsync(
                    1,
                    ct =>
                    {
                        secondEntered.TrySetResult(true);
                        return Task.FromResult(true);
                    },
                    CancellationToken.None);

            try
            {
                await Task.Delay(300);

                // Druga transakcja nadal czeka.
                Assert.False(secondEntered.Task.IsCompleted);
            }
            finally
            {
                // Zwalniamy pierwszą blokadę.
                releaseFirst.TrySetResult(true);
            }

            await Task.WhenAll(firstTask, secondTask)
                .WaitAsync(TimeSpan.FromSeconds(15));

            Assert.True(secondEntered.Task.IsCompleted);
        });
    }

    private static async Task WithDatabaseAsync(
        Func<DbContextOptions<WarehouseDbContext>, Task> test)
    {
        var name = "WarehouseLockTest_" +
                   Guid.NewGuid().ToString("N");

        var connectionString = new SqlConnectionStringBuilder
        {
            DataSource = @"(localdb)\MSSQLLocalDB",
            InitialCatalog = name,
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            ConnectTimeout = 30
        }.ConnectionString;

        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        var migrated = false;

        try
        {
            await using (var setup =
                new WarehouseDbContext(options))
            {
                await setup.Database.MigrateAsync();
                migrated = true;
            }

            await test(options);
        }
        finally
        {
            if (migrated)
            {
                await using var cleanup =
                    new WarehouseDbContext(options);

                await cleanup.Database.EnsureDeletedAsync();
            }
        }
    }
}

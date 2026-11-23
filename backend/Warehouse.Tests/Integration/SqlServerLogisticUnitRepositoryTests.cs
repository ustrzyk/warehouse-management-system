
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.DTOs.LogisticUnits;
using Warehouse.Application.Exceptions;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;
using Warehouse.Infrastructure.Persistence;
using Warehouse.Infrastructure.Repositories;
using Xunit;

namespace Warehouse.Tests.Integration;

[Trait("Category", "SqlServer")]
public class SqlServerLogisticUnitRepositoryTests
{
    private static readonly CancellationToken Cancellation =
        CancellationToken.None;

    // Test 1: real database insert and read.

    [Fact]
    public async Task Add_ValidPallet_PersistsAndCanBeRead()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            var code = NewCode();

            int savedId;

            await using (var context =
                new WarehouseDbContext(options))
            {
                var repository =
                    new LogisticUnitRepository(context);

                var unit = NewUnit(code);

                await repository.AddAsync(unit, Cancellation);
                await repository.SaveChangesAsync(Cancellation);

                savedId = unit.Id;

                Assert.True(savedId > 0);
                Assert.NotEmpty(unit.RowVersion);
            }

            // New DbContext = read data from actual SQL Server.

            await using (var context =
                new WarehouseDbContext(options))
            {
                var repository =
                    new LogisticUnitRepository(context);

                var saved = await repository.GetByIdAsync(
                    savedId, Cancellation);

                Assert.NotNull(saved);
                Assert.Equal(code, saved!.Code);
                Assert.Equal(LogisticUnitStatus.Created, saved.Status);
                Assert.Null(saved.StorageLocationId);
                Assert.NotEmpty(saved.RowVersion);

                Assert.True(
                    await repository.CodeExistsAsync(
                        code, Cancellation));
            }
        });
    }

    // Test 2: SQL Server unique index prevents duplicate LE.

    [Fact]
    public async Task Add_DuplicatePalletCode_ThrowsConflict()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            var code = NewCode();

            await using (var firstContext =
                new WarehouseDbContext(options))
            {
                var firstRepository =
                    new LogisticUnitRepository(firstContext);

                await firstRepository.AddAsync(
                    NewUnit(code), Cancellation);

                await firstRepository.SaveChangesAsync(Cancellation);
            }

            await using (var secondContext =
                new WarehouseDbContext(options))
            {
                var secondRepository =
                    new LogisticUnitRepository(secondContext);

                await secondRepository.AddAsync(
                    NewUnit(code), Cancellation);

                await Assert.ThrowsAsync<
                    DuplicateLogisticUnitCodeException>(
                    () => secondRepository.SaveChangesAsync(
                        Cancellation));
            }
        });
    }

    // Test 3: RowVersion prevents a lost update.

    [Fact]
    public async Task Update_StaleRowVersion_ThrowsConcurrencyException()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            int unitId;

            await using (var setupContext =
                new WarehouseDbContext(options))
            {
                var repository =
                    new LogisticUnitRepository(setupContext);

                var unit = NewUnit(NewCode());

                await repository.AddAsync(unit, Cancellation);
                await repository.SaveChangesAsync(Cancellation);

                unitId = unit.Id;
            }

            await using var contextA =
                new WarehouseDbContext(options);

            await using var contextB =
                new WarehouseDbContext(options);

            var repositoryA =
                new LogisticUnitRepository(contextA);

            var repositoryB =
                new LogisticUnitRepository(contextB);

            var unitA = await repositoryA.GetTrackedByIdAsync(
                unitId, Cancellation);

            var unitB = await repositoryB.GetTrackedByIdAsync(
                unitId, Cancellation);

            Assert.NotNull(unitA);
            Assert.NotNull(unitB);

            var originalVersion = unitA!.RowVersion.ToArray();

            // First operation successfully updates the pallet.
            unitA.Revision++;

            await repositoryA.SaveChangesAsync(Cancellation);

            Assert.False(
                originalVersion.SequenceEqual(unitA.RowVersion));

            // Second operation still has the old RowVersion.
            unitB!.Revision++;

            await Assert.ThrowsAsync<
                ConcurrentLogisticUnitModificationException>(
                () => repositoryB.SaveChangesAsync(Cancellation));

            await using var verificationContext =
                new WarehouseDbContext(options);

            var verificationRepository =
                new LogisticUnitRepository(verificationContext);

            var actual = await verificationRepository.GetByIdAsync(
                unitId, Cancellation);

            Assert.NotNull(actual);
            Assert.Equal(1, actual!.Revision);
        });
    }

    // Test 4: SQL filtering, sorting and pagination.

    [Fact]
    public async Task GetPage_FiltersOrdersAndCountsPallets()
    {
        await RunWithTemporaryDatabaseAsync(async options =>
        {
            var prefix = "IT-" +
                Guid.NewGuid().ToString("N")[..12] + "-";

            await using (var setupContext =
                new WarehouseDbContext(options))
            {
                var repository =
                    new LogisticUnitRepository(setupContext);

                foreach (var suffix in new[] { "C", "A", "B" })
                {
                    await repository.AddAsync(
                        NewUnit(prefix + suffix),
                        Cancellation);
                }

                await repository.AddAsync(
                    NewUnit(NewCode()), Cancellation);

                await repository.SaveChangesAsync(Cancellation);
            }

            await using var queryContext =
                new WarehouseDbContext(options);

            var queryRepository =
                new LogisticUnitRepository(queryContext);

            var firstPage = await queryRepository.GetPageAsync(
                new LogisticUnitListQuery
                {
                    Search = prefix,
                    Page = 1,
                    PageSize = 2
                },
                Cancellation);

            Assert.Equal(3, firstPage.TotalCount);
            Assert.Equal(2, firstPage.Items.Count);
            Assert.Equal(prefix + "A", firstPage.Items[0].Code);
            Assert.Equal(prefix + "B", firstPage.Items[1].Code);

            var secondPage = await queryRepository.GetPageAsync(
                new LogisticUnitListQuery
                {
                    Search = prefix,
                    Page = 2,
                    PageSize = 2
                },
                Cancellation);

            Assert.Equal(3, secondPage.TotalCount);
            Assert.Single(secondPage.Items);
            Assert.Equal(prefix + "C", secondPage.Items[0].Code);
        });
    }

    // ---------- Helpers ----------

    private static LogisticUnit NewUnit(string code)
    {
        return new LogisticUnit
        {
            Code = code,
            Status = LogisticUnitStatus.Created,
            CreatedAtUtc = DateTime.UtcNow,
            Revision = 0
        };
    }

    private static string NewCode()
    {
        return "LE-IT-" + Guid.NewGuid().ToString("N");
    }

    private static async Task RunWithTemporaryDatabaseAsync(
        Func<DbContextOptions<WarehouseDbContext>, Task> test)
    {
        // A new random database is created for EACH test.
        // Never use the application's WarehouseDb database.

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
                // Apply the real application's EF Core migrations.
                await setup.Database.MigrateAsync();

                migrationCompleted = true;
            }

            await test(options);
        }
        finally
        {
            if (migrationCompleted)
            {
                // Delete only the temporary database
                // generated by this specific test.

                await using var cleanup =
                    new WarehouseDbContext(options);

                await cleanup.Database.EnsureDeletedAsync();
            }
        }
    }
}

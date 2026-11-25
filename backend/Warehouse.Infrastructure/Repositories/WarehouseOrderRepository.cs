
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.DTOs.WarehouseOrders;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Domain.Entities;
using Warehouse.Infrastructure.Persistence;
using Warehouse.Domain.Enums;


namespace Warehouse.Infrastructure.Repositories;

public class WarehouseOrderRepository : IWarehouseOrderRepository
{
    private readonly WarehouseDbContext _context;

    public WarehouseOrderRepository(WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task<T> ExecuteWithWarehouseReleaseLockAsync<T>(
        int warehouseId,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

        var resourceParameter = new SqlParameter(
            "@LockResource", SqlDbType.NVarChar, 255)
        {
            Value = $"WMS.OrderRelease.Warehouse.{warehouseId}"
        };

        var resultParameter = new SqlParameter(
            "@LockResult", SqlDbType.Int)
        {
            Direction = ParameterDirection.Output
        };

        await _context.Database.ExecuteSqlRawAsync(
            """
            EXEC @LockResult = sys.sp_getapplock
                @Resource = @LockResource,
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 10000;
            """,
            new object[]
            {
                resourceParameter,
                resultParameter
            },
            cancellationToken);

        if (resultParameter.Value is not int result || result < 0)
        {
            throw new ConcurrentWarehouseOrderModificationException(
                new InvalidOperationException(
                    $"Nie udało się uzyskać blokady magazynu. " +
                    $"Kod SQL Server: {resultParameter.Value}"));
        }

        var value = await operation(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return value;
    }

    public async Task<(
        IReadOnlyList<WarehouseOrder> Items,
        int TotalCount)> GetPageAsync(
        WarehouseOrderListQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<WarehouseOrder> orders =
            _context.WarehouseOrders
                .AsNoTracking()
                .Include(x => x.Warehouse);

        if (query.WarehouseId.HasValue)
        {
            var warehouseId = query.WarehouseId.Value;

            orders = orders.Where(
                x => x.WarehouseId == warehouseId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            orders = orders.Where(x =>
                x.Number.Contains(search) ||
                (x.CustomerName != null &&
                 x.CustomerName.Contains(search)));
        }

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;

            orders = orders.Where(
                x => x.Status == status);
        }

        var totalCount = await orders.CountAsync(
            cancellationToken);

        var items = await orders
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<WarehouseOrder?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _context.WarehouseOrders
            .AsNoTracking()
            .Include(x => x.Warehouse)
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public Task<WarehouseOrder?> GetTrackedByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _context.WarehouseOrders
            .Include(x => x.Warehouse)
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public Task<bool> WarehouseIsActiveAsync(
        int warehouseId,
        CancellationToken cancellationToken)
    {
        return _context.Warehouses
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == warehouseId && x.IsActive,
                cancellationToken);
    }

    public Task<bool> NumberExistsAsync(
        string number,
        CancellationToken cancellationToken)
    {
        return _context.WarehouseOrders
            .AsNoTracking()
            .AnyAsync(
                x => x.Number == number,
                cancellationToken);
    }

    public Task<bool> ProductIsActiveAsync(
        int productId,
        CancellationToken cancellationToken)
    {
        return _context.Products
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == productId && x.IsActive,
                cancellationToken);
    }

    public Task<bool> BatchIsActiveForProductAsync(
        int batchId,
        int productId,
        CancellationToken cancellationToken)
    {
        return _context.ProductBatches
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == batchId
                     && x.ProductId == productId
                     && x.IsActive,
                cancellationToken);
    }

    public Task<bool> ItemExistsAsync(
        int orderId,
        int productId,
        int? batchId,
        CancellationToken cancellationToken)
    {
        return _context.WarehouseOrderItems
            .AsNoTracking()
            .AnyAsync(
                x => x.WarehouseOrderId == orderId
                     && x.ProductId == productId
                     && x.ProductBatchId == batchId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<WarehouseOrderItem>> GetItemsAsync(
        int orderId,
        CancellationToken cancellationToken)
    {
        return await _context.WarehouseOrderItems
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.ProductBatch)
            .Where(x => x.WarehouseOrderId == orderId)
            .OrderBy(x => x.Product.Code)
            .ThenBy(x => x.ProductBatchId)
            .ToListAsync(cancellationToken);
    }

    public Task<WarehouseOrderItem?> GetItemByIdAsync(
        int orderId,
        int itemId,
        CancellationToken cancellationToken)
    {
        return _context.WarehouseOrderItems
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.ProductBatch)
            .FirstOrDefaultAsync(
                x => x.WarehouseOrderId == orderId
                     && x.Id == itemId,
                cancellationToken);
    }

    public async Task AddAsync(
        WarehouseOrder order,
        CancellationToken cancellationToken)
    {
        await _context.WarehouseOrders.AddAsync(
            order, cancellationToken);
    }

    public async Task AddItemAsync(
        WarehouseOrderItem item,
        CancellationToken cancellationToken)
    {
        await _context.WarehouseOrderItems.AddAsync(
            item, cancellationToken);
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrentWarehouseOrderModificationException(ex);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException
            { Number: 2601 or 2627 })
        {
            throw new DuplicateWarehouseOrderException(ex);
        }
    }

    public Task<WarehouseOrder?> GetTrackedWithItemsAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _context.WarehouseOrders
            .Include(x => x.Warehouse)
            .Include(x => x.Items)
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public async Task<IReadOnlyList<WarehouseStockBalance>>
        GetAvailableStockAsync(
            int warehouseId,
            IReadOnlyCollection<int> productIds,
            CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
            return Array.Empty<WarehouseStockBalance>();

        var ids = productIds.ToArray();

        return await _context.LogisticUnitItems
            .AsNoTracking()
            .Where(x =>
                ids.Contains(x.ProductId) &&
                x.LogisticUnit.Status == LogisticUnitStatus.Stored &&
                x.LogisticUnit.StorageLocation != null &&
                x.LogisticUnit.StorageLocation.IsActive &&
                x.LogisticUnit.StorageLocation.WarehouseZone.IsActive &&
                x.LogisticUnit.StorageLocation.WarehouseZone.Warehouse.IsActive &&
                x.LogisticUnit.StorageLocation.WarehouseZone.WarehouseId
                    == warehouseId)
            .GroupBy(x => new
            {
                x.ProductId,
                x.ProductBatchId
            })
            .Select(group => new WarehouseStockBalance(
                group.Key.ProductId,
                group.Key.ProductBatchId,
                group.Sum(x => x.Quantity)))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WarehouseStockBalance>>
        GetOutstandingReleasedStockAsync(
            int warehouseId,
            IReadOnlyCollection<int> productIds,
            CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
            return Array.Empty<WarehouseStockBalance>();

        var ids = productIds.ToArray();

        var lines = await _context.WarehouseOrderItems
            .AsNoTracking()
            .Where(x =>
                x.WarehouseOrder.WarehouseId == warehouseId &&
                x.WarehouseOrder.Status == WarehouseOrderStatus.Released &&
                ids.Contains(x.ProductId))
            .Select(x => new
            {
                x.ProductId,
                x.ProductBatchId,
                x.QuantityRequested,

                QuantityPicked = _context.PickingOperations
                    .Where(p => p.WarehouseOrderItemId == x.Id)
                    .Sum(p => (decimal?)p.Quantity) ?? 0m
            })
            .ToListAsync(cancellationToken);

        return lines
            .GroupBy(x => new
            {
                x.ProductId,
                x.ProductBatchId
            })
            .Select(group => new WarehouseStockBalance(
                group.Key.ProductId,
                group.Key.ProductBatchId,
                group.Sum(x => Math.Max(
                    0m,
                    x.QuantityRequested - x.QuantityPicked))))
            .ToList();
    }

    public async Task<IReadOnlyList<WarehouseOrderItemProgress>>
        GetItemProgressAsync(
            int orderId,
            CancellationToken cancellationToken)
    {
        return await _context.WarehouseOrderItems
            .AsNoTracking()
            .Where(x => x.WarehouseOrderId == orderId)
            .OrderBy(x => x.Id)
            .Select(x => new WarehouseOrderItemProgress(
                x.Id,
                x.QuantityRequested,
                _context.PickingOperations
                    .Where(p => p.WarehouseOrderItemId == x.Id)
                    .Sum(p => (decimal?)p.Quantity) ?? 0m))
            .ToListAsync(cancellationToken);
    }

    public Task<DateTime?> GetLastPickedAtUtcAsync(
        int orderId,
        CancellationToken cancellationToken)
    {
        return _context.PickingOperations
            .AsNoTracking()
            .Where(x => x.WarehouseOrderId == orderId)
            .MaxAsync(
                x => (DateTime?)x.PickedAtUtc,
                cancellationToken);
    }



}

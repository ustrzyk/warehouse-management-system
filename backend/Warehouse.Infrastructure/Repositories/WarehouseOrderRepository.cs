
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.DTOs.WarehouseOrders;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Domain.Entities;
using Warehouse.Infrastructure.Persistence;

namespace Warehouse.Infrastructure.Repositories;

public class WarehouseOrderRepository : IWarehouseOrderRepository
{
    private readonly WarehouseDbContext _context;

    public WarehouseOrderRepository(WarehouseDbContext context)
    {
        _context = context;
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
}

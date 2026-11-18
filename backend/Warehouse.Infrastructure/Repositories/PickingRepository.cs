
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.DTOs.Picking;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Domain.Entities;
using Warehouse.Infrastructure.Persistence;

namespace Warehouse.Infrastructure.Repositories;

public class PickingRepository : IPickingRepository
{
    private readonly WarehouseDbContext _context;

    public PickingRepository(WarehouseDbContext context)
    {
        _context = context;
    }

    public Task<bool> OrderExistsAsync(
        int orderId,
        CancellationToken cancellationToken)
    {
        return _context.WarehouseOrders
            .AsNoTracking()
            .AnyAsync(x => x.Id == orderId, cancellationToken);
    }

    public Task<PickingOperation?> GetByRequestIdAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        return _context.PickingOperations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.RequestId == requestId,
                cancellationToken);
    }

    public Task<WarehouseOrderItem?> GetTrackedOrderItemAsync(
        int orderId,
        int orderItemId,
        CancellationToken cancellationToken)
    {
        return _context.WarehouseOrderItems
            .Include(x => x.WarehouseOrder)
                .ThenInclude(x => x.Warehouse)
            .FirstOrDefaultAsync(
                x => x.Id == orderItemId &&
                     x.WarehouseOrderId == orderId,
                cancellationToken);
    }

    public Task<LogisticUnitItem?> GetTrackedPalletItemAsync(
        int logisticUnitItemId,
        CancellationToken cancellationToken)
    {
        return _context.LogisticUnitItems
            .Include(x => x.Product)
            .Include(x => x.ProductBatch)
            .Include(x => x.LogisticUnit)
                .ThenInclude(x => x.StorageLocation)
                    .ThenInclude(x => x!.WarehouseZone)
                        .ThenInclude(x => x.Warehouse)
            .FirstOrDefaultAsync(
                x => x.Id == logisticUnitItemId,
                cancellationToken);
    }

    public async Task<decimal> GetPickedQuantityAsync(
        int orderItemId,
        CancellationToken cancellationToken)
    {
        var total = await _context.PickingOperations
            .AsNoTracking()
            .Where(x => x.WarehouseOrderItemId == orderItemId)
            .SumAsync(
                x => (decimal?)x.Quantity,
                cancellationToken);

        return total ?? 0m;
    }

    public async Task AddAsync(
        PickingOperation operation,
        CancellationToken cancellationToken)
    {
        await _context.PickingOperations.AddAsync(
            operation, cancellationToken);
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
            throw new ConcurrentPickingOperationException(ex);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException
            { Number: 2601 or 2627 })
        {
            throw new PickingConflictException(
                "Konflikt zapisu operacji kompletacji.",
                ex);
        }
    }

    public async Task<(
        IReadOnlyList<PickingOperation> Items,
        int TotalCount)> GetPageAsync(
        int orderId,
        PickingListQuery query,
        CancellationToken cancellationToken)
    {
        var operations = _context.PickingOperations
            .AsNoTracking()
            .Where(x => x.WarehouseOrderId == orderId);

        var totalCount = await operations.CountAsync(
            cancellationToken);

        var items = await operations
            .OrderByDescending(x => x.PickedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<PickingOperation?> GetByIdAsync(
        int orderId,
        int pickingId,
        CancellationToken cancellationToken)
    {
        return _context.PickingOperations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.WarehouseOrderId == orderId &&
                     x.Id == pickingId,
                cancellationToken);
    }
}

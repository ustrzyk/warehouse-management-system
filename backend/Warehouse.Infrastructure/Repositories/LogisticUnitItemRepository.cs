
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Domain.Entities;
using Warehouse.Infrastructure.Persistence;

namespace Warehouse.Infrastructure.Repositories;

public class LogisticUnitItemRepository
    : ILogisticUnitItemRepository
{
    private readonly WarehouseDbContext _context;

    public LogisticUnitItemRepository(
        WarehouseDbContext context)
    {
        _context = context;
    }

    public Task<LogisticUnit?> GetLogisticUnitAsync(
        int logisticUnitId,
        CancellationToken cancellationToken)
    {
        return _context.LogisticUnits
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == logisticUnitId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<LogisticUnitItem>> GetItemsAsync(
        int logisticUnitId,
        CancellationToken cancellationToken)
    {
        return await _context.LogisticUnitItems
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.ProductBatch)
            .Where(x => x.LogisticUnitId == logisticUnitId)
            .OrderBy(x => x.Product.Code)
            .ThenBy(x => x.ProductBatchId)
            .ToListAsync(cancellationToken);
    }

    public Task<LogisticUnitItem?> GetByIdAsync(
        int logisticUnitId,
        int itemId,
        CancellationToken cancellationToken)
    {
        return _context.LogisticUnitItems
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.ProductBatch)
            .FirstOrDefaultAsync(
                x => x.LogisticUnitId == logisticUnitId
                     && x.Id == itemId,
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
        int productBatchId,
        int productId,
        CancellationToken cancellationToken)
    {
        return _context.ProductBatches
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == productBatchId
                     && x.ProductId == productId
                     && x.IsActive,
                cancellationToken);
    }

    public Task<bool> ItemExistsAsync(
        int logisticUnitId,
        int productId,
        int? productBatchId,
        CancellationToken cancellationToken)
    {
        return _context.LogisticUnitItems
            .AsNoTracking()
            .AnyAsync(
                x => x.LogisticUnitId == logisticUnitId
                     && x.ProductId == productId
                     && x.ProductBatchId == productBatchId,
                cancellationToken);
    }

    public async Task AddAsync(
        LogisticUnitItem item,
        CancellationToken cancellationToken)
    {
        await _context.LogisticUnitItems.AddAsync(
            item, cancellationToken);
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException
            { Number: 2601 or 2627 })
        {
            throw new DuplicateLogisticUnitItemException(ex);
        }
    }
}

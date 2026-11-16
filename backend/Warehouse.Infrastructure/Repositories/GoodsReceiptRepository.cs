
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.DTOs.GoodsReceipts;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Domain.Entities;
using Warehouse.Infrastructure.Persistence;

namespace Warehouse.Infrastructure.Repositories;

public class GoodsReceiptRepository : IGoodsReceiptRepository
{
    private readonly WarehouseDbContext _context;

    public GoodsReceiptRepository(WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task<(
        IReadOnlyList<GoodsReceipt> Items,
        int TotalCount)> GetPageAsync(
        GoodsReceiptListQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<GoodsReceipt> receipts =
            _context.GoodsReceipts
                .AsNoTracking()
                .Include(x => x.Warehouse);

        if (query.WarehouseId.HasValue)
        {
            var warehouseId = query.WarehouseId.Value;

            receipts = receipts.Where(
                x => x.WarehouseId == warehouseId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            receipts = receipts.Where(x =>
                x.Number.Contains(search) ||
                (x.SupplierName != null &&
                 x.SupplierName.Contains(search)));
        }

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;

            receipts = receipts.Where(
                x => x.Status == status);
        }

        var totalCount = await receipts.CountAsync(
            cancellationToken);

        var items = await receipts
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<GoodsReceipt?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _context.GoodsReceipts
            .AsNoTracking()
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
        return _context.GoodsReceipts
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
        int receiptId,
        int productId,
        int? batchId,
        CancellationToken cancellationToken)
    {
        return _context.GoodsReceiptItems
            .AsNoTracking()
            .AnyAsync(
                x => x.GoodsReceiptId == receiptId
                     && x.ProductId == productId
                     && x.ProductBatchId == batchId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<GoodsReceiptItem>> GetItemsAsync(
        int receiptId,
        CancellationToken cancellationToken)
    {
        return await _context.GoodsReceiptItems
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.ProductBatch)
            .Where(x => x.GoodsReceiptId == receiptId)
            .OrderBy(x => x.Product.Code)
            .ThenBy(x => x.ProductBatchId)
            .ToListAsync(cancellationToken);
    }

    public Task<GoodsReceiptItem?> GetItemByIdAsync(
        int receiptId,
        int itemId,
        CancellationToken cancellationToken)
    {
        return _context.GoodsReceiptItems
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.ProductBatch)
            .FirstOrDefaultAsync(
                x => x.GoodsReceiptId == receiptId
                     && x.Id == itemId,
                cancellationToken);
    }

    public async Task AddAsync(
        GoodsReceipt receipt,
        CancellationToken cancellationToken)
    {
        await _context.GoodsReceipts.AddAsync(
            receipt, cancellationToken);
    }

    public async Task AddItemAsync(
        GoodsReceiptItem item,
        CancellationToken cancellationToken)
    {
        await _context.GoodsReceiptItems.AddAsync(
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
            throw new ConcurrentGoodsReceiptModificationException(ex);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException
            { Number: 2601 or 2627 })
        {
            throw new DuplicateGoodsReceiptException(ex);
        }
    }



    public Task<GoodsReceipt?> GetTrackedByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _context.GoodsReceipts
            .Include(x => x.Warehouse)
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public Task<GoodsReceipt?> GetTrackedWithItemsAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _context.GoodsReceipts
            .Include(x => x.Warehouse)
            .Include(x => x.Items)
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public async Task AddLogisticUnitAsync(
        LogisticUnit unit,
        CancellationToken cancellationToken)
    {
        await _context.LogisticUnits.AddAsync(
            unit, cancellationToken);
    }


}

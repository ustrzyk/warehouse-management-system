
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.DTOs.ProductBatches;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Domain.Entities;
using Warehouse.Infrastructure.Persistence;

namespace Warehouse.Infrastructure.Repositories;

public class ProductBatchRepository : IProductBatchRepository
{
    private readonly WarehouseDbContext _context;

    public ProductBatchRepository(WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task<(
        IReadOnlyList<ProductBatch> Items,
        int TotalCount)> GetPageAsync(
        ProductBatchListQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<ProductBatch> batches =
            _context.ProductBatches
                .AsNoTracking()
                .Include(x => x.Product);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            batches = batches.Where(x =>
                x.BatchNumber.Contains(search) ||
                x.Product.Code.Contains(search) ||
                x.Product.Name.Contains(search));
        }

        if (query.ProductId.HasValue)
        {
            var productId = query.ProductId.Value;

            batches = batches.Where(
                x => x.ProductId == productId);
        }

        if (query.IsActive.HasValue)
        {
            var isActive = query.IsActive.Value;

            batches = batches.Where(
                x => x.IsActive == isActive);
        }

        var totalCount = await batches
            .CountAsync(cancellationToken);

        batches = (query.SortBy.ToLowerInvariant(),
                   query.Descending) switch
        {
            ("productiondate", true) =>
                batches.OrderByDescending(x => x.ProductionDate)
                    .ThenBy(x => x.Id),

            ("productiondate", false) =>
                batches.OrderBy(x => x.ProductionDate)
                    .ThenBy(x => x.Id),

            ("expirationdate", true) =>
                batches.OrderByDescending(x => x.ExpirationDate)
                    .ThenBy(x => x.Id),

            ("expirationdate", false) =>
                batches.OrderBy(x => x.ExpirationDate)
                    .ThenBy(x => x.Id),

            ("batchnumber", true) =>
                batches.OrderByDescending(x => x.BatchNumber)
                    .ThenBy(x => x.Id),

            _ =>
                batches.OrderBy(x => x.BatchNumber)
                    .ThenBy(x => x.Id)
        };

        var items = await batches
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<ProductBatch?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _context.ProductBatches
            .AsNoTracking()
            .Include(x => x.Product)
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public Task<ProductBatch?> GetTrackedByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _context.ProductBatches
            .FirstOrDefaultAsync(
                x => x.Id == id,
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

    public Task<bool> BatchNumberExistsAsync(
        int productId,
        string batchNumber,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        return _context.ProductBatches
            .AsNoTracking()
            .AnyAsync(
                x => x.ProductId == productId &&
                     x.BatchNumber == batchNumber &&
                     (!excludeId.HasValue ||
                      x.Id != excludeId.Value),
                cancellationToken);
    }

    public async Task AddAsync(
        ProductBatch batch,
        CancellationToken cancellationToken)
    {
        await _context.ProductBatches.AddAsync(
            batch, cancellationToken);
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
            throw new DuplicateProductBatchException(ex);
        }
    }
}

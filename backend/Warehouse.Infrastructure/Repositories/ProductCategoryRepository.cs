
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.DTOs.ProductCategories;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Domain.Entities;
using Warehouse.Infrastructure.Persistence;

namespace Warehouse.Infrastructure.Repositories;

public class ProductCategoryRepository
    : IProductCategoryRepository
{
    private readonly WarehouseDbContext _context;

    public ProductCategoryRepository(
        WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task<(
        IReadOnlyList<ProductCategory> Items,
        int TotalCount)> GetPageAsync(
            ProductCategoryListQuery query,
            CancellationToken cancellationToken)
    {
        IQueryable<ProductCategory> categories =
            _context.ProductCategories.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            categories = categories.Where(x =>
                x.Code.Contains(search) ||
                x.Name.Contains(search));
        }

        if (query.IsActive.HasValue)
        {
            var isActive = query.IsActive.Value;

            categories = categories.Where(
                x => x.IsActive == isActive);
        }

        var totalCount = await categories
            .CountAsync(cancellationToken);

        categories = (
            query.SortBy.ToLowerInvariant(),
            query.Descending
        ) switch
        {
            ("code", true) =>
                categories.OrderByDescending(x => x.Code)
                    .ThenBy(x => x.Id),

            ("code", false) =>
                categories.OrderBy(x => x.Code)
                    .ThenBy(x => x.Id),

            ("name", true) =>
                categories.OrderByDescending(x => x.Name)
                    .ThenBy(x => x.Id),

            _ =>
                categories.OrderBy(x => x.Name)
                    .ThenBy(x => x.Id)
        };

        var items = await categories
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<ProductCategory?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return await _context.ProductCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public async Task<ProductCategory?> GetTrackedByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return await _context.ProductCategories
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public async Task<bool> CodeExistsAsync(
        string code,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        var query = _context.ProductCategories
            .AsNoTracking()
            .Where(x => x.Code == code);

        if (excludeId.HasValue)
        {
            query = query.Where(
                x => x.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(
        ProductCategory category,
        CancellationToken cancellationToken)
    {
        await _context.ProductCategories.AddAsync(
            category,
            cancellationToken);
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
            throw new DuplicateProductCategoryCodeException(ex);
        }
    }
}

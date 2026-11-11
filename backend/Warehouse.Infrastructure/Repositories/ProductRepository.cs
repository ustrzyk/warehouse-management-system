
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.DTOs.Products;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Domain.Entities;
using Warehouse.Infrastructure.Persistence;

namespace Warehouse.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly WarehouseDbContext _context;

    public ProductRepository(WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task<(
        IReadOnlyList<Product> Items,
        int TotalCount)> GetPageAsync(
        ProductListQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<Product> products =
            _context.Products
                .AsNoTracking()
                .Include(x => x.ProductCategory)
                .Include(x => x.UnitOfMeasure);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            products = products.Where(x =>
                x.Code.Contains(search) ||
                x.Name.Contains(search) ||
                (x.Ean != null && x.Ean.Contains(search)));
        }

        if (query.IsActive.HasValue)
        {
            var isActive = query.IsActive.Value;
            products = products.Where(x => x.IsActive == isActive);
        }

        if (query.ProductCategoryId.HasValue)
        {
            var categoryId = query.ProductCategoryId.Value;
            products = products.Where(
                x => x.ProductCategoryId == categoryId);
        }

        if (query.UnitOfMeasureId.HasValue)
        {
            var unitId = query.UnitOfMeasureId.Value;
            products = products.Where(
                x => x.UnitOfMeasureId == unitId);
        }

        var totalCount = await products
            .CountAsync(cancellationToken);

        products = (query.SortBy.ToLowerInvariant(),
                    query.Descending) switch
        {
            ("code", true) =>
                products.OrderByDescending(x => x.Code)
                    .ThenBy(x => x.Id),

            ("code", false) =>
                products.OrderBy(x => x.Code)
                    .ThenBy(x => x.Id),

            ("ean", true) =>
                products.OrderByDescending(x => x.Ean)
                    .ThenBy(x => x.Id),

            ("ean", false) =>
                products.OrderBy(x => x.Ean)
                    .ThenBy(x => x.Id),

            ("name", true) =>
                products.OrderByDescending(x => x.Name)
                    .ThenBy(x => x.Id),

            _ =>
                products.OrderBy(x => x.Name)
                    .ThenBy(x => x.Id)
        };

        var items = await products
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<Product?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _context.Products
            .AsNoTracking()
            .Include(x => x.ProductCategory)
            .Include(x => x.UnitOfMeasure)
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public Task<Product?> GetTrackedByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _context.Products
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public Task<bool> CodeExistsAsync(
        string code,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        return _context.Products
            .AsNoTracking()
            .AnyAsync(
                x => x.Code == code &&
                    (!excludeId.HasValue ||
                     x.Id != excludeId.Value),
                cancellationToken);
    }

    public Task<bool> CategoryIsActiveAsync(
        int categoryId,
        CancellationToken cancellationToken)
    {
        return _context.ProductCategories
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == categoryId && x.IsActive,
                cancellationToken);
    }

    public Task<bool> UnitIsActiveAsync(
        int unitId,
        CancellationToken cancellationToken)
    {
        return _context.UnitsOfMeasure
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == unitId && x.IsActive,
                cancellationToken);
    }

    public async Task AddAsync(
        Product product,
        CancellationToken cancellationToken)
    {
        await _context.Products.AddAsync(
            product, cancellationToken);
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
            throw new DuplicateProductCodeException(ex);
        }
    }
}

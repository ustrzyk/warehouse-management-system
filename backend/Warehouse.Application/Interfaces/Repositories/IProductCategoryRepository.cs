
using Warehouse.Application.DTOs.ProductCategories;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Interfaces.Repositories;

public interface IProductCategoryRepository
{
    Task<(IReadOnlyList<ProductCategory> Items, int TotalCount)>
        GetPageAsync(
            ProductCategoryListQuery query,
            CancellationToken cancellationToken);

    Task<ProductCategory?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<ProductCategory?> GetTrackedByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(
        string code,
        int? excludeId,
        CancellationToken cancellationToken);

    Task AddAsync(
        ProductCategory category,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(
        CancellationToken cancellationToken);
}

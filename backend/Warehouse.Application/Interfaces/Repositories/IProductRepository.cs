
using Warehouse.Application.DTOs.Products;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Interfaces.Repositories;

public interface IProductRepository
{
    Task<(IReadOnlyList<Product> Items, int TotalCount)>
        GetPageAsync(
            ProductListQuery query,
            CancellationToken cancellationToken);

    Task<Product?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<Product?> GetTrackedByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(
        string code,
        int? excludeId,
        CancellationToken cancellationToken);

    Task<bool> CategoryIsActiveAsync(
        int categoryId,
        CancellationToken cancellationToken);

    Task<bool> UnitIsActiveAsync(
        int unitId,
        CancellationToken cancellationToken);

    Task AddAsync(
        Product product,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(
        CancellationToken cancellationToken);
}

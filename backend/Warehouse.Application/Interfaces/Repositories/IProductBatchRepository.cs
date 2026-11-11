
using Warehouse.Application.DTOs.ProductBatches;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Interfaces.Repositories;

public interface IProductBatchRepository
{
    Task<(IReadOnlyList<ProductBatch> Items, int TotalCount)>
        GetPageAsync(
            ProductBatchListQuery query,
            CancellationToken cancellationToken);

    Task<ProductBatch?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<ProductBatch?> GetTrackedByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<bool> ProductIsActiveAsync(
        int productId,
        CancellationToken cancellationToken);

    Task<bool> BatchNumberExistsAsync(
        int productId,
        string batchNumber,
        int? excludeId,
        CancellationToken cancellationToken);

    Task AddAsync(
        ProductBatch batch,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(
        CancellationToken cancellationToken);
}

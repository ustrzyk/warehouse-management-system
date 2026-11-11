
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.ProductBatches;

namespace Warehouse.Application.Interfaces.Services;

public interface IProductBatchService
{
    Task<PagedResult<ProductBatchDto>> GetPageAsync(
        ProductBatchListQuery query,
        CancellationToken cancellationToken);

    Task<ProductBatchDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<ProductBatchDto> CreateAsync(
        CreateProductBatchRequest request,
        CancellationToken cancellationToken);

    Task<ProductBatchDto?> UpdateAsync(
        int id,
        UpdateProductBatchRequest request,
        CancellationToken cancellationToken);

    Task<bool> DeactivateAsync(
        int id,
        CancellationToken cancellationToken);
}


using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.Products;

namespace Warehouse.Application.Interfaces.Services;

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetPageAsync(
        ProductListQuery query,
        CancellationToken cancellationToken);

    Task<ProductDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<ProductDto> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken);

    Task<ProductDto?> UpdateAsync(
        int id,
        UpdateProductRequest request,
        CancellationToken cancellationToken);

    Task<bool> DeactivateAsync(
        int id,
        CancellationToken cancellationToken);
}

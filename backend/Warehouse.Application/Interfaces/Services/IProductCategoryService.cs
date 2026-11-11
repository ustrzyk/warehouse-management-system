
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.ProductCategories;

namespace Warehouse.Application.Interfaces.Services;

public interface IProductCategoryService
{
    Task<PagedResult<ProductCategoryDto>> GetPageAsync(
        ProductCategoryListQuery query,
        CancellationToken cancellationToken);

    Task<ProductCategoryDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<ProductCategoryDto> CreateAsync(
        CreateProductCategoryRequest request,
        CancellationToken cancellationToken);

    Task<ProductCategoryDto?> UpdateAsync(
        int id,
        UpdateProductCategoryRequest request,
        CancellationToken cancellationToken);

    Task<bool> DeactivateAsync(
        int id,
        CancellationToken cancellationToken);
}

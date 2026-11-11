
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.ProductCategories;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Application.Interfaces.Services;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Services;

public class ProductCategoryService : IProductCategoryService
{
    private readonly IProductCategoryRepository _repository;

    public ProductCategoryService(
        IProductCategoryRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<ProductCategoryDto>> GetPageAsync(
        ProductCategoryListQuery query,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) =
            await _repository.GetPageAsync(
                query,
                cancellationToken);

        return new PagedResult<ProductCategoryDto>(
            items.Select(MapToDto).ToList(),
            totalCount,
            query.Page,
            query.PageSize);
    }

    public async Task<ProductCategoryDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var category = await _repository.GetByIdAsync(
            id,
            cancellationToken);

        return category is null ? null : MapToDto(category);
    }

    public async Task<ProductCategoryDto> CreateAsync(
        CreateProductCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();

        if (await _repository.CodeExistsAsync(
            code, null, cancellationToken))
        {
            throw new DuplicateProductCategoryCodeException();
        }

        var category = new ProductCategory
        {
            Code = code,
            Name = request.Name.Trim(),
            Description = NormalizeDescription(request.Description),
            IsActive = true
        };

        await _repository.AddAsync(category, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return MapToDto(category);
    }

    public async Task<ProductCategoryDto?> UpdateAsync(
        int id,
        UpdateProductCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var category = await _repository.GetTrackedByIdAsync(
            id,
            cancellationToken);

        if (category is null)
        {
            return null;
        }

        var code = request.Code.Trim().ToUpperInvariant();

        if (await _repository.CodeExistsAsync(
            code, id, cancellationToken))
        {
            throw new DuplicateProductCategoryCodeException();
        }

        category.Code = code;
        category.Name = request.Name.Trim();
        category.Description =
            NormalizeDescription(request.Description);
        category.IsActive = request.IsActive;

        await _repository.SaveChangesAsync(cancellationToken);

        return MapToDto(category);
    }

    public async Task<bool> DeactivateAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var category = await _repository.GetTrackedByIdAsync(
            id,
            cancellationToken);

        if (category is null)
        {
            return false;
        }

        if (category.IsActive)
        {
            category.IsActive = false;
            await _repository.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    private static string? NormalizeDescription(
        string? description)
    {
        return string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();
    }

    private static ProductCategoryDto MapToDto(
        ProductCategory category)
    {
        return new ProductCategoryDto(
            category.Id,
            category.Code,
            category.Name,
            category.Description,
            category.IsActive);
    }
}

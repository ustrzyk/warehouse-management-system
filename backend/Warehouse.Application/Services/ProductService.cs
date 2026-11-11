
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.Products;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Application.Interfaces.Services;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _repository;

    public ProductService(IProductRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<ProductDto>> GetPageAsync(
        ProductListQuery query,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) =
            await _repository.GetPageAsync(
                query, cancellationToken);

        return new PagedResult<ProductDto>(
            items.Select(MapToDto).ToList(),
            totalCount,
            query.Page,
            query.PageSize);
    }

    public async Task<ProductDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var product = await _repository.GetByIdAsync(
            id, cancellationToken);

        return product is null ? null : MapToDto(product);
    }

    public async Task<ProductDto> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();

        if (await _repository.CodeExistsAsync(
            code, null, cancellationToken))
        {
            throw new DuplicateProductCodeException();
        }

        await ValidateReferencesAsync(
            request.ProductCategoryId,
            request.UnitOfMeasureId,
            cancellationToken);

        var product = new Product
        {
            Code = code,
            Name = request.Name.Trim(),
            Description = NormalizeOptional(request.Description),
            Ean = NormalizeOptional(request.Ean),
            ProductCategoryId = request.ProductCategoryId,
            UnitOfMeasureId = request.UnitOfMeasureId,
            IsActive = true
        };

        await _repository.AddAsync(
            product, cancellationToken);

        await _repository.SaveChangesAsync(
            cancellationToken);

        var created = await _repository.GetByIdAsync(
            product.Id, cancellationToken);

        return MapToDto(created
            ?? throw new InvalidOperationException(
                "Created product could not be loaded."));
    }

    public async Task<ProductDto?> UpdateAsync(
        int id,
        UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _repository.GetTrackedByIdAsync(
            id, cancellationToken);

        if (product is null)
            return null;

        var code = request.Code.Trim().ToUpperInvariant();

        if (await _repository.CodeExistsAsync(
            code, id, cancellationToken))
        {
            throw new DuplicateProductCodeException();
        }

        await ValidateReferencesAsync(
            request.ProductCategoryId,
            request.UnitOfMeasureId,
            cancellationToken);

        product.Code = code;
        product.Name = request.Name.Trim();
        product.Description =
            NormalizeOptional(request.Description);
        product.Ean = NormalizeOptional(request.Ean);
        product.ProductCategoryId = request.ProductCategoryId;
        product.UnitOfMeasureId = request.UnitOfMeasureId;
        product.IsActive = request.IsActive;

        await _repository.SaveChangesAsync(
            cancellationToken);

        var updated = await _repository.GetByIdAsync(
            id, cancellationToken);

        return updated is null ? null : MapToDto(updated);
    }

    public async Task<bool> DeactivateAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var product = await _repository.GetTrackedByIdAsync(
            id, cancellationToken);

        if (product is null)
            return false;

        if (product.IsActive)
        {
            product.IsActive = false;

            await _repository.SaveChangesAsync(
                cancellationToken);
        }

        return true;
    }

    private async Task ValidateReferencesAsync(
        int categoryId,
        int unitId,
        CancellationToken cancellationToken)
    {
        if (!await _repository.CategoryIsActiveAsync(
            categoryId, cancellationToken))
        {
            throw new InvalidProductReferenceException(
                "Kategoria nie istnieje lub jest nieaktywna.");
        }

        if (!await _repository.UnitIsActiveAsync(
            unitId, cancellationToken))
        {
            throw new InvalidProductReferenceException(
                "Jednostka miary nie istnieje lub jest nieaktywna.");
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static ProductDto MapToDto(Product product)
    {
        return new ProductDto(
            product.Id,
            product.Code,
            product.Name,
            product.Description,
            product.Ean,
            product.ProductCategoryId,
            product.ProductCategory.Code,
            product.ProductCategory.Name,
            product.UnitOfMeasureId,
            product.UnitOfMeasure.Code,
            product.UnitOfMeasure.Name,
            product.UnitOfMeasure.Symbol,
            product.IsActive);
    }
}

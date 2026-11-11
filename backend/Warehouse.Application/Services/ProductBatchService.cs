
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.ProductBatches;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Application.Interfaces.Services;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Services;

public class ProductBatchService : IProductBatchService
{
    private readonly IProductBatchRepository _repository;

    public ProductBatchService(
        IProductBatchRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<ProductBatchDto>> GetPageAsync(
        ProductBatchListQuery query,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) =
            await _repository.GetPageAsync(
                query, cancellationToken);

        return new PagedResult<ProductBatchDto>(
            items.Select(MapToDto).ToList(),
            totalCount,
            query.Page,
            query.PageSize);
    }

    public async Task<ProductBatchDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var batch = await _repository.GetByIdAsync(
            id, cancellationToken);

        return batch is null ? null : MapToDto(batch);
    }

    public async Task<ProductBatchDto> CreateAsync(
        CreateProductBatchRequest request,
        CancellationToken cancellationToken)
    {
        var batchNumber = NormalizeBatchNumber(
            request.BatchNumber);

        ValidateDates(
            request.ProductionDate,
            request.ExpirationDate);

        if (!await _repository.ProductIsActiveAsync(
            request.ProductId, cancellationToken))
        {
            throw new InvalidProductBatchException(
                "Produkt nie istnieje lub jest nieaktywny.");
        }

        if (await _repository.BatchNumberExistsAsync(
            request.ProductId,
            batchNumber,
            null,
            cancellationToken))
        {
            throw new DuplicateProductBatchException();
        }

        var batch = new ProductBatch
        {
            ProductId = request.ProductId,
            BatchNumber = batchNumber,
            ProductionDate = request.ProductionDate,
            ExpirationDate = request.ExpirationDate,
            IsActive = true
        };

        await _repository.AddAsync(
            batch, cancellationToken);

        await _repository.SaveChangesAsync(
            cancellationToken);

        var created = await _repository.GetByIdAsync(
            batch.Id, cancellationToken);

        return MapToDto(created
            ?? throw new InvalidOperationException(
                "Created batch could not be loaded."));
    }

    public async Task<ProductBatchDto?> UpdateAsync(
        int id,
        UpdateProductBatchRequest request,
        CancellationToken cancellationToken)
    {
        var batch = await _repository.GetTrackedByIdAsync(
            id, cancellationToken);

        if (batch is null)
            return null;

        var batchNumber = NormalizeBatchNumber(
            request.BatchNumber);

        ValidateDates(
            request.ProductionDate,
            request.ExpirationDate);

        if (request.IsActive &&
            !await _repository.ProductIsActiveAsync(
                batch.ProductId, cancellationToken))
        {
            throw new InvalidProductBatchException(
                "Nie można aktywować partii nieaktywnego produktu.");
        }

        if (await _repository.BatchNumberExistsAsync(
            batch.ProductId,
            batchNumber,
            id,
            cancellationToken))
        {
            throw new DuplicateProductBatchException();
        }

        batch.BatchNumber = batchNumber;
        batch.ProductionDate = request.ProductionDate;
        batch.ExpirationDate = request.ExpirationDate;
        batch.IsActive = request.IsActive;

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
        var batch = await _repository.GetTrackedByIdAsync(
            id, cancellationToken);

        if (batch is null)
            return false;

        if (batch.IsActive)
        {
            batch.IsActive = false;

            await _repository.SaveChangesAsync(
                cancellationToken);
        }

        return true;
    }

    private static string NormalizeBatchNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidProductBatchException(
                "Numer partii nie może być pusty.");
        }

        return value.Trim().ToUpperInvariant();
    }

    private static void ValidateDates(
        DateOnly? productionDate,
        DateOnly? expirationDate)
    {
        if (productionDate.HasValue &&
            expirationDate.HasValue &&
            productionDate.Value > expirationDate.Value)
        {
            throw new InvalidProductBatchException(
                "Data produkcji nie może być późniejsza " +
                "od daty ważności.");
        }
    }

    private static ProductBatchDto MapToDto(
        ProductBatch batch)
    {
        return new ProductBatchDto(
            batch.Id,
            batch.ProductId,
            batch.Product.Code,
            batch.Product.Name,
            batch.BatchNumber,
            batch.ProductionDate,
            batch.ExpirationDate,
            batch.IsActive);
    }
}

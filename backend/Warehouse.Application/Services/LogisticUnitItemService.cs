
using Warehouse.Application.DTOs.LogisticUnitItems;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Application.Interfaces.Services;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Services;

public class LogisticUnitItemService : ILogisticUnitItemService
{
    private readonly ILogisticUnitItemRepository _repository;

    public LogisticUnitItemService(
        ILogisticUnitItemRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<LogisticUnitItemDto>?>
        GetItemsAsync(
            int logisticUnitId,
            CancellationToken cancellationToken)
    {
        var unit = await _repository.GetLogisticUnitAsync(
            logisticUnitId, cancellationToken);

        if (unit is null)
            return null;

        var items = await _repository.GetItemsAsync(
            logisticUnitId, cancellationToken);

        return items.Select(MapToDto).ToList();
    }

    public async Task<LogisticUnitItemDto?> GetByIdAsync(
        int logisticUnitId,
        int itemId,
        CancellationToken cancellationToken)
    {
        var item = await _repository.GetByIdAsync(
            logisticUnitId, itemId, cancellationToken);

        return item is null ? null : MapToDto(item);
    }

    public async Task<LogisticUnitItemDto?> CreateAsync(
        int logisticUnitId,
        CreateLogisticUnitItemRequest request,
        CancellationToken cancellationToken)
    {
        var unit = await _repository.GetLogisticUnitAsync(
            logisticUnitId, cancellationToken);

        if (unit is null)
            return null;

        // Na razie dodajemy pozycje tylko do nowej palety.
        if (unit.Status != LogisticUnitStatus.Created)
        {
            throw new InvalidLogisticUnitItemException(
                "Mozna dodawac pozycje tylko do palety Created.");
        }

        if (request.Quantity <= 0 ||
            request.Quantity > 999999999999999.999m ||
            decimal.Round(request.Quantity, 3) != request.Quantity)
        {
            throw new InvalidLogisticUnitItemException(
                "Ilosc musi byc dodatnia i miec maksymalnie " +
                "3 miejsca po przecinku.");
        }

        if (!await _repository.ProductIsActiveAsync(
            request.ProductId, cancellationToken))
        {
            throw new InvalidLogisticUnitItemException(
                "Produkt nie istnieje lub jest nieaktywny.");
        }

        if (request.ProductBatchId.HasValue)
        {
            var validBatch =
                await _repository.BatchIsActiveForProductAsync(
                    request.ProductBatchId.Value,
                    request.ProductId,
                    cancellationToken);

            if (!validBatch)
            {
                throw new InvalidLogisticUnitItemException(
                    "Partia nie istnieje, jest nieaktywna " +
                    "lub nalezy do innego produktu.");
            }
        }

        if (await _repository.ItemExistsAsync(
            logisticUnitId,
            request.ProductId,
            request.ProductBatchId,
            cancellationToken))
        {
            throw new DuplicateLogisticUnitItemException();
        }

        var item = new LogisticUnitItem
        {
            LogisticUnitId = logisticUnitId,
            ProductId = request.ProductId,
            ProductBatchId = request.ProductBatchId,
            Quantity = request.Quantity
        };

        await _repository.AddAsync(
            item, cancellationToken);

        await _repository.SaveChangesAsync(
            cancellationToken);

        var created = await _repository.GetByIdAsync(
            logisticUnitId,
            item.Id,
            cancellationToken);

        return MapToDto(created
            ?? throw new InvalidOperationException(
                "Created logistic unit item could not be loaded."));
    }

    private static LogisticUnitItemDto MapToDto(
        LogisticUnitItem item)
    {
        return new LogisticUnitItemDto(
            item.Id,
            item.LogisticUnitId,
            item.ProductId,
            item.Product.Code,
            item.Product.Name,
            item.ProductBatchId,
            item.ProductBatch?.BatchNumber,
            item.Quantity);
    }
}


using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.WarehouseOrders;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Application.Interfaces.Services;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Services;

public class WarehouseOrderService : IWarehouseOrderService
{
    private readonly IWarehouseOrderRepository _repository;

    public WarehouseOrderService(
        IWarehouseOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<WarehouseOrderDto>> GetPageAsync(
        WarehouseOrderListQuery query,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) =
            await _repository.GetPageAsync(
                query, cancellationToken);

        return new PagedResult<WarehouseOrderDto>(
            items.Select(MapToDto).ToList(),
            totalCount,
            query.Page,
            query.PageSize);
    }

    public async Task<WarehouseOrderDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdAsync(
            id, cancellationToken);

        return order is null ? null : MapToDto(order);
    }

    public async Task<WarehouseOrderDto> CreateAsync(
        CreateWarehouseOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Number))
        {
            throw new InvalidWarehouseOrderException(
                "Numer zamowienia nie moze byc pusty.");
        }

        if (!await _repository.WarehouseIsActiveAsync(
            request.WarehouseId, cancellationToken))
        {
            throw new InvalidWarehouseOrderException(
                "Magazyn nie istnieje lub jest nieaktywny.");
        }

        var number = request.Number.Trim().ToUpperInvariant();

        if (await _repository.NumberExistsAsync(
            number, cancellationToken))
        {
            throw new DuplicateWarehouseOrderException();
        }

        var order = new WarehouseOrder
        {
            Number = number,
            WarehouseId = request.WarehouseId,
            CustomerName = string.IsNullOrWhiteSpace(
                request.CustomerName)
                    ? null
                    : request.CustomerName.Trim(),
            Status = WarehouseOrderStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            Revision = 0
        };

        await _repository.AddAsync(
            order, cancellationToken);

        await _repository.SaveChangesAsync(cancellationToken);

        var created = await _repository.GetByIdAsync(
            order.Id, cancellationToken);

        return created is null
            ? throw new InvalidOperationException(
                "Created warehouse order could not be loaded.")
            : MapToDto(created);
    }

    public async Task<IReadOnlyList<WarehouseOrderItemDto>?>
        GetItemsAsync(
            int orderId,
            CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdAsync(
            orderId, cancellationToken);

        if (order is null)
            return null;

        var items = await _repository.GetItemsAsync(
            orderId, cancellationToken);

        return items.Select(MapItemToDto).ToList();
    }

    public async Task<WarehouseOrderItemDto?> AddItemAsync(
        int orderId,
        AddWarehouseOrderItemRequest request,
        CancellationToken cancellationToken)
    {
        var order = await _repository.GetTrackedByIdAsync(
            orderId, cancellationToken);

        if (order is null)
            return null;

        if (order.Status != WarehouseOrderStatus.Draft)
        {
            throw new InvalidWarehouseOrderException(
                "Pozycje mozna dodawac tylko do zamowienia Draft.");
        }

        if (!order.Warehouse.IsActive)
        {
            throw new InvalidWarehouseOrderException(
                "Magazyn zamowienia jest nieaktywny.");
        }

        if (request.QuantityRequested <= 0 ||
            request.QuantityRequested > 999999999999999.999m ||
            decimal.Round(request.QuantityRequested, 3)
                != request.QuantityRequested)
        {
            throw new InvalidWarehouseOrderException(
                "Ilosc musi byc dodatnia i miec maksymalnie " +
                "3 miejsca po przecinku.");
        }

        if (!await _repository.ProductIsActiveAsync(
            request.ProductId, cancellationToken))
        {
            throw new InvalidWarehouseOrderException(
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
                throw new InvalidWarehouseOrderException(
                    "Partia nie istnieje, jest nieaktywna " +
                    "lub nalezy do innego produktu.");
            }
        }

        if (await _repository.ItemExistsAsync(
            orderId,
            request.ProductId,
            request.ProductBatchId,
            cancellationToken))
        {
            throw new DuplicateWarehouseOrderException();
        }

        var item = new WarehouseOrderItem
        {
            WarehouseOrderId = orderId,
            ProductId = request.ProductId,
            ProductBatchId = request.ProductBatchId,
            QuantityRequested = request.QuantityRequested
        };

        // Aktualizacja zamowienia wywoluje zmiane RowVersion.
        order.Revision++;

        await _repository.AddItemAsync(
            item, cancellationToken);

        await _repository.SaveChangesAsync(
            cancellationToken);

        var created = await _repository.GetItemByIdAsync(
            orderId, item.Id, cancellationToken);

        return created is null
            ? throw new InvalidOperationException(
                "Created warehouse order item could not be loaded.")
            : MapItemToDto(created);
    }

    private static WarehouseOrderDto MapToDto(
        WarehouseOrder order)
    {
        return new WarehouseOrderDto(
            order.Id,
            order.Number,
            order.WarehouseId,
            order.Warehouse.Code,
            order.CustomerName,
            order.Status.ToString(),
            order.CreatedAtUtc,
            order.Revision);
    }

    private static WarehouseOrderItemDto MapItemToDto(
        WarehouseOrderItem item)
    {
        return new WarehouseOrderItemDto(
            item.Id,
            item.WarehouseOrderId,
            item.ProductId,
            item.Product.Code,
            item.Product.Name,
            item.ProductBatchId,
            item.ProductBatch?.BatchNumber,
            item.QuantityRequested);
    }
}

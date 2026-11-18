
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

    public async Task<WarehouseOrderDto?> ReleaseAsync(
        int id,
        CancellationToken cancellationToken)
    {
        // 1. Pobierz zamowienie z pozycjami.
        var order = await _repository.GetTrackedWithItemsAsync(
            id, cancellationToken);

        if (order is null)
            return null;

        // 2. Tylko Draft mozna przekazac do kompletacji.
        if (order.Status != WarehouseOrderStatus.Draft)
        {
            throw new WarehouseOrderAlreadyReleasedException();
        }

        // 3. Sprawdz magazyn.
        if (!order.Warehouse.IsActive)
        {
            throw new InvalidWarehouseOrderException(
                "Magazyn zamowienia jest nieaktywny.");
        }

        // 4. Zamowienie musi miec przynajmniej jedna pozycje.
        if (order.Items.Count == 0)
        {
            throw new InvalidWarehouseOrderException(
                "Nie mozna przekazac pustego zamowienia.");
        }

        // 5. Sprawdz wszystkie pozycje zamowienia.
        foreach (var item in order.Items)
        {
            if (item.QuantityRequested <= 0 ||
                item.QuantityRequested > 999999999999999.999m ||
                decimal.Round(item.QuantityRequested, 3)
                    != item.QuantityRequested)
            {
                throw new InvalidWarehouseOrderException(
                    "Zamowienie zawiera nieprawidlowa ilosc.");
            }

            if (!await _repository.ProductIsActiveAsync(
                item.ProductId, cancellationToken))
            {
                throw new InvalidWarehouseOrderException(
                    $"Produkt ID {item.ProductId} jest nieaktywny.");
            }

            if (item.ProductBatchId.HasValue)
            {
                var validBatch =
                    await _repository.BatchIsActiveForProductAsync(
                        item.ProductBatchId.Value,
                        item.ProductId,
                        cancellationToken);

                if (!validBatch)
                {
                    throw new InvalidWarehouseOrderException(
                        $"Nieprawidlowa partia produktu ID {item.ProductId}.");
                }
            }
        }

        // 6. Pobierz dostepne ilosci z palet Stored.
        var productIds = order.Items
            .Select(x => x.ProductId)
            .Distinct()
            .ToArray();

        var stock = await _repository.GetAvailableStockAsync(
            order.WarehouseId,
            productIds,
            cancellationToken);

        // 7. Zweryfikuj dostepnosc kazdego artykulu.
        foreach (var productGroup in order.Items.GroupBy(x => x.ProductId))
        {
            var availableForProduct = stock
                .Where(x => x.ProductId == productGroup.Key)
                .ToList();

            var totalAvailable = availableForProduct
                .Sum(x => x.Quantity);

            var totalRequested = productGroup
                .Sum(x => x.QuantityRequested);

            if (totalAvailable < totalRequested)
            {
                throw new InsufficientWarehouseStockException(
                    $"Za malo towaru dla produktu ID {productGroup.Key}. " +
                    $"Dostepne: {totalAvailable}, " +
                    $"zamowione: {totalRequested}.");
            }

            // Jesli zamowienie wskazuje konkretna partie,
            // jej ilosc rowniez musi byc wystarczajaca.
            foreach (var item in productGroup
                .Where(x => x.ProductBatchId.HasValue))
            {
                var availableBatch = availableForProduct
                    .Where(x => x.ProductBatchId == item.ProductBatchId)
                    .Sum(x => x.Quantity);

                if (availableBatch < item.QuantityRequested)
                {
                    throw new InsufficientWarehouseStockException(
                        $"Za malo towaru produktu ID {item.ProductId}, " +
                        $"partia ID {item.ProductBatchId}. " +
                        $"Dostepne: {availableBatch}, " +
                        $"zamowione: {item.QuantityRequested}.");
                }
            }
        }

        // 8. Przekaz zamowienie do kompletacji.
        order.Status = WarehouseOrderStatus.Released;
        order.ReleasedAtUtc = DateTime.UtcNow;
        order.Revision++;

        // 9. Jeden zapis, z kontrola RowVersion.
        await _repository.SaveChangesAsync(cancellationToken);

        return MapToDto(order);
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
            order.Revision,
            order.ReleasedAtUtc);
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

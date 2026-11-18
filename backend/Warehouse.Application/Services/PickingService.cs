
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.Picking;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Application.Interfaces.Services;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Services;

public class PickingService : IPickingService
{
    private readonly IPickingRepository _repository;

    public PickingService(IPickingRepository repository)
    {
        _repository = repository;
    }

    public async Task<PickingOperationDto?> CreateAsync(
        int orderId,
        CreatePickingRequest request,
        CancellationToken cancellationToken)
    {
        // 1. Walidacja danych wejsciowych.
        if (request.RequestId == Guid.Empty)
        {
            throw new InvalidPickingOperationException(
                "RequestId nie moze byc pusty.");
        }

        if (request.Quantity <= 0 ||
            request.Quantity > 999999999999999.999m ||
            decimal.Round(request.Quantity, 3) != request.Quantity)
        {
            throw new InvalidPickingOperationException(
                "Ilosc musi byc dodatnia i miec maksymalnie " +
                "3 miejsca po przecinku.");
        }

        // 2. Ochrona przed powtornym wyslaniem tego samego zadania.
        var existing = await _repository.GetByRequestIdAsync(
            request.RequestId, cancellationToken);

        if (existing is not null)
        {
            if (existing.WarehouseOrderId != orderId ||
                existing.WarehouseOrderItemId
                    != request.WarehouseOrderItemId ||
                existing.LogisticUnitItemId
                    != request.LogisticUnitItemId ||
                existing.Quantity != request.Quantity)
            {
                throw new PickingConflictException(
                    "RequestId zostal juz wykorzystany " +
                    "dla innej operacji.");
            }

            return MapToDto(existing);
        }

        // 3. Pobierz pozycje zamowienia.
        var orderItem = await _repository.GetTrackedOrderItemAsync(
            orderId,
            request.WarehouseOrderItemId,
            cancellationToken);

        if (orderItem is null)
            return null;

        var order = orderItem.WarehouseOrder;

        if (order.Status != WarehouseOrderStatus.Released)
        {
            throw new InvalidPickingOperationException(
                "Kompletowac mozna tylko zamowienia Released.");
        }

        if (!order.Warehouse.IsActive)
        {
            throw new InvalidPickingOperationException(
                "Magazyn zamowienia jest nieaktywny.");
        }

        // 4. Pobierz pozycje konkretnej palety.
        var palletItem = await _repository.GetTrackedPalletItemAsync(
            request.LogisticUnitItemId,
            cancellationToken);

        if (palletItem is null)
        {
            throw new InvalidPickingOperationException(
                "Pozycja palety nie istnieje.");
        }

        var pallet = palletItem.LogisticUnit;
        var location = pallet.StorageLocation;

        if (pallet.Status != LogisticUnitStatus.Stored ||
            location is null)
        {
            throw new InvalidPickingOperationException(
                "Towar musi znajdowac sie na odlozonej palecie Stored.");
        }

        // 5. Sprawdz lokalizacje oraz magazyn.
        if (!location.IsActive ||
            !location.WarehouseZone.IsActive ||
            !location.WarehouseZone.Warehouse.IsActive ||
            location.WarehouseZone.WarehouseId != order.WarehouseId)
        {
            throw new InvalidPickingOperationException(
                "Paleta znajduje sie w niewlasciwym " +
                "lub nieaktywnym magazynie/lokalizacji.");
        }

        // 6. Sprawdz artykul i partie.
        if (palletItem.ProductId != orderItem.ProductId)
        {
            throw new InvalidPickingOperationException(
                "Artykul na palecie nie odpowiada zamowieniu.");
        }

        if (orderItem.ProductBatchId.HasValue &&
            palletItem.ProductBatchId != orderItem.ProductBatchId)
        {
            throw new InvalidPickingOperationException(
                "Partia na palecie nie odpowiada zamowieniu.");
        }

        if (!palletItem.Product.IsActive ||
            (palletItem.ProductBatchId.HasValue &&
             palletItem.ProductBatch?.IsActive != true))
        {
            throw new InvalidPickingOperationException(
                "Produkt lub partia jest nieaktywna.");
        }

        // 7. Sprawdz, ile juz pobrano na te pozycje zamowienia.
        var pickedSoFar = await _repository.GetPickedQuantityAsync(
            orderItem.Id,
            cancellationToken);

        var remainingToPick =
            orderItem.QuantityRequested - pickedSoFar;

        if (request.Quantity > remainingToPick)
        {
            throw new PickingConflictException(
                $"Przekroczono ilosc zamowiona. " +
                $"Pozostalo do pobrania: {Math.Max(0m, remainingToPick)}.");
        }

        // 8. Sprawdz aktualny stan konkretnej palety.
        if (request.Quantity > palletItem.Quantity)
        {
            throw new PickingConflictException(
                $"Za malo towaru na palecie. " +
                $"Dostepne: {palletItem.Quantity}.");
        }

        // 9. Przygotuj historyczny zapis kompletacji.
        var operation = new PickingOperation
        {
            RequestId = request.RequestId,
            WarehouseOrderId = orderId,
            WarehouseOrderItemId = orderItem.Id,

            LogisticUnitId = pallet.Id,
            LogisticUnitItemId = palletItem.Id,

            LogisticUnitCode = pallet.Code,
            ProductCode = palletItem.Product.Code,
            BatchNumber = palletItem.ProductBatch?.BatchNumber,
            SourceLocationCode = location.Code,

            Quantity = request.Quantity,
            PickedAtUtc = DateTime.UtcNow
        };

        // 10. Zmniejsz ilosc pozostala na palecie.
        palletItem.Quantity -= request.Quantity;

        // 11. Wymus aktualizacje RowVersion zamowienia i palety.
        order.Revision++;
        pallet.Revision++;

        // 12. Dodaj historie i zapisz wszystko atomowo.
        await _repository.AddAsync(
            operation, cancellationToken);

        await _repository.SaveChangesAsync(
            cancellationToken);

        var saved = await _repository.GetByIdAsync(
            orderId,
            operation.Id,
            cancellationToken);

        return saved is null
            ? throw new InvalidOperationException(
                "Created picking operation could not be loaded.")
            : MapToDto(saved);
    }

    public async Task<PagedResult<PickingOperationDto>?>
        GetPageAsync(
            int orderId,
            PickingListQuery query,
            CancellationToken cancellationToken)
    {
        if (!await _repository.OrderExistsAsync(
            orderId, cancellationToken))
        {
            return null;
        }

        var (items, totalCount) = await _repository.GetPageAsync(
            orderId, query, cancellationToken);

        return new PagedResult<PickingOperationDto>(
            items.Select(MapToDto).ToList(),
            totalCount,
            query.Page,
            query.PageSize);
    }

    public async Task<PickingOperationDto?> GetByIdAsync(
        int orderId,
        int pickingId,
        CancellationToken cancellationToken)
    {
        var operation = await _repository.GetByIdAsync(
            orderId, pickingId, cancellationToken);

        return operation is null ? null : MapToDto(operation);
    }

    private static PickingOperationDto MapToDto(
        PickingOperation operation)
    {
        return new PickingOperationDto(
            operation.Id,
            operation.RequestId,
            operation.WarehouseOrderId,
            operation.WarehouseOrderItemId,
            operation.LogisticUnitId,
            operation.LogisticUnitItemId,
            operation.LogisticUnitCode,
            operation.ProductCode,
            operation.BatchNumber,
            operation.SourceLocationCode,
            operation.Quantity,
            operation.PickedAtUtc);
    }
}

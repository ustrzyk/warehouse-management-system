
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.GoodsReceipts;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Application.Interfaces.Services;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Services;

public class GoodsReceiptService : IGoodsReceiptService
{
    private readonly IGoodsReceiptRepository _repository;

    public GoodsReceiptService(
        IGoodsReceiptRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<GoodsReceiptDto>> GetPageAsync(
        GoodsReceiptListQuery query,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) =
            await _repository.GetPageAsync(
                query, cancellationToken);

        return new PagedResult<GoodsReceiptDto>(
            items.Select(MapToDto).ToList(),
            totalCount,
            query.Page,
            query.PageSize);
    }

    public async Task<GoodsReceiptDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var receipt = await _repository.GetByIdAsync(
            id, cancellationToken);

        return receipt is null ? null : MapToDto(receipt);
    }

    public async Task<GoodsReceiptDto> CreateAsync(
        CreateGoodsReceiptRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Number))
        {
            throw new InvalidGoodsReceiptException(
                "Numer dokumentu nie moze byc pusty.");
        }

        if (!await _repository.WarehouseIsActiveAsync(
            request.WarehouseId, cancellationToken))
        {
            throw new InvalidGoodsReceiptException(
                "Magazyn nie istnieje lub jest nieaktywny.");
        }

        var number = request.Number.Trim().ToUpperInvariant();

        if (await _repository.NumberExistsAsync(
            number, cancellationToken))
        {
            throw new DuplicateGoodsReceiptException();
        }

        var receipt = new GoodsReceipt
        {
            Number = number,
            WarehouseId = request.WarehouseId,
            SupplierName = string.IsNullOrWhiteSpace(
                request.SupplierName)
                    ? null
                    : request.SupplierName.Trim(),
            Status = GoodsReceiptStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _repository.AddAsync(
            receipt, cancellationToken);

        await _repository.SaveChangesAsync(cancellationToken);

        var created = await _repository.GetByIdAsync(
            receipt.Id, cancellationToken);

        return created is null
            ? throw new InvalidOperationException(
                "Created goods receipt could not be loaded.")
            : MapToDto(created);
    }

    public async Task<IReadOnlyList<GoodsReceiptItemDto>?>
        GetItemsAsync(
            int receiptId,
            CancellationToken cancellationToken)
    {
        var receipt = await _repository.GetByIdAsync(
            receiptId, cancellationToken);

        if (receipt is null)
            return null;

        var items = await _repository.GetItemsAsync(
            receiptId, cancellationToken);

        return items.Select(MapItemToDto).ToList();
    }

    public async Task<GoodsReceiptItemDto?> AddItemAsync(
        int receiptId,
        AddGoodsReceiptItemRequest request,
        CancellationToken cancellationToken)
    {

        var receipt = await _repository.GetTrackedByIdAsync(
            receiptId, cancellationToken);

        if (receipt is null)
            return null;

        if (receipt.Status != GoodsReceiptStatus.Draft)
        {
            throw new InvalidGoodsReceiptException(
                "Pozycje mozna dodawac tylko do dokumentu Draft.");
        }

        if (request.Quantity <= 0 ||
            request.Quantity > 999999999999999.999m ||
            decimal.Round(request.Quantity, 3) != request.Quantity)
        {
            throw new InvalidGoodsReceiptException(
                "Ilosc musi byc dodatnia i miec maksymalnie " +
                "3 miejsca po przecinku.");
        }

        if (!await _repository.ProductIsActiveAsync(
            request.ProductId, cancellationToken))
        {
            throw new InvalidGoodsReceiptException(
                "Produkt nie istnieje lub jest nieaktywny.");
        }

        if (request.ProductBatchId.HasValue)
        {
            var batchValid =
                await _repository.BatchIsActiveForProductAsync(
                    request.ProductBatchId.Value,
                    request.ProductId,
                    cancellationToken);

            if (!batchValid)
            {
                throw new InvalidGoodsReceiptException(
                    "Partia jest nieaktywna, nie istnieje " +
                    "lub nalezy do innego produktu.");
            }
        }

        if (await _repository.ItemExistsAsync(
            receiptId,
            request.ProductId,
            request.ProductBatchId,
            cancellationToken))
        {
            throw new DuplicateGoodsReceiptException();
        }

        var item = new GoodsReceiptItem
        {
            GoodsReceiptId = receiptId,
            ProductId = request.ProductId,
            ProductBatchId = request.ProductBatchId,
            Quantity = request.Quantity
        };


        receipt.Revision++;
        await _repository.AddItemAsync(
            item, cancellationToken);

        await _repository.SaveChangesAsync(cancellationToken);

        var created = await _repository.GetItemByIdAsync(
            receiptId, item.Id, cancellationToken);

        return created is null
            ? throw new InvalidOperationException(
                "Created goods receipt item could not be loaded.")
            : MapItemToDto(created);
    }


    public async Task<GoodsReceiptDto?> ConfirmAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var receipt = await _repository.GetTrackedWithItemsAsync(
            id, cancellationToken);

        if (receipt is null)
            return null;

        if (receipt.Status != GoodsReceiptStatus.Draft ||
            receipt.LogisticUnitId.HasValue)
        {
            throw new GoodsReceiptAlreadyConfirmedException();
        }

        if (!receipt.Warehouse.IsActive)
        {
            throw new InvalidGoodsReceiptException(
                "Nie mozna zatwierdzic przyjecia do nieaktywnego magazynu.");
        }

        if (receipt.Items.Count == 0)
        {
            throw new InvalidGoodsReceiptException(
                "Nie mozna zatwierdzic dokumentu bez pozycji.");
        }

        foreach (var item in receipt.Items)
        {
            if (item.Quantity <= 0 ||
                item.Quantity > 999999999999999.999m ||
                decimal.Round(item.Quantity, 3) != item.Quantity)
            {
                throw new InvalidGoodsReceiptException(
                    "Dokument zawiera nieprawidlowa ilosc.");
            }

            if (!await _repository.ProductIsActiveAsync(
                item.ProductId, cancellationToken))
            {
                throw new InvalidGoodsReceiptException(
                    $"Produkt o ID {item.ProductId} jest nieaktywny " +
                    "lub nie istnieje.");
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
                    throw new InvalidGoodsReceiptException(
                        "Dokument zawiera nieaktywna lub " +
                        "nieprawidlowa partie produktu.");
                }
            }
        }

        var logisticUnit = new LogisticUnit
        {
            Code = $"LE-GR-{receipt.Id:D8}",
            Status = LogisticUnitStatus.Created,
            StorageLocationId = null,
            CreatedAtUtc = DateTime.UtcNow,

            Items = receipt.Items.Select(item =>
                new LogisticUnitItem
                {
                    ProductId = item.ProductId,
                    ProductBatchId = item.ProductBatchId,
                    Quantity = item.Quantity
                }).ToList()
        };

        receipt.LogisticUnit = logisticUnit;
        receipt.Status = GoodsReceiptStatus.Received;
        receipt.ReceivedAtUtc = DateTime.UtcNow;
        receipt.Revision++;

        await _repository.AddLogisticUnitAsync(
            logisticUnit, cancellationToken);

        // Jeden atomowy zapis:
        // paleta, pozycje, status i powiazanie dokumentu.
        await _repository.SaveChangesAsync(
            cancellationToken);

        return MapToDto(receipt);
    }



    private static GoodsReceiptDto MapToDto(
        GoodsReceipt receipt)
    {
        return new GoodsReceiptDto(
            receipt.Id,
            receipt.Number,
            receipt.WarehouseId,
            receipt.Warehouse.Code,
            receipt.SupplierName,
            receipt.Status.ToString(),
            receipt.CreatedAtUtc,
            receipt.ReceivedAtUtc,
            receipt.LogisticUnitId);
    }


    private static GoodsReceiptItemDto MapItemToDto(
        GoodsReceiptItem item)
    {
        return new GoodsReceiptItemDto(
            item.Id,
            item.GoodsReceiptId,
            item.ProductId,
            item.Product.Code,
            item.Product.Name,
            item.ProductBatchId,
            item.ProductBatch?.BatchNumber,
            item.Quantity);
    }
}

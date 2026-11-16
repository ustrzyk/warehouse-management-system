
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

    // Pobranie wszystkich pozycji palety
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

    // Pobranie jednej pozycji palety
    public async Task<LogisticUnitItemDto?> GetByIdAsync(
        int logisticUnitId,
        int itemId,
        CancellationToken cancellationToken)
    {
        var item = await _repository.GetByIdAsync(
            logisticUnitId,
            itemId,
            cancellationToken);

        return item is null ? null : MapToDto(item);
    }

    // Dodanie nowej pozycji do palety
    public async Task<LogisticUnitItemDto?> CreateAsync(
        int logisticUnitId,
        CreateLogisticUnitItemRequest request,
        CancellationToken cancellationToken)
    {
        // 1. Sprawdzenie, czy paleta istnieje
        var unit = await _repository.GetLogisticUnitAsync(
            logisticUnitId, cancellationToken);

        if (unit is null)
            return null;

        // 2. Pozycje mozna dodawac tylko do palety Created
        if (unit.Status != LogisticUnitStatus.Created)
        {
            throw new InvalidLogisticUnitItemException(
                "Mozna dodawac pozycje tylko do palety Created.");
        }

        // 3. NOWE ZABEZPIECZENIE:
        // Nie pozwalamy zmieniac zawartosci palety,
        // ktora powstala z zatwierdzonego GoodsReceipt.
        if (await _repository.IsGeneratedByGoodsReceiptAsync(
            logisticUnitId, cancellationToken))
        {
            throw new InvalidLogisticUnitItemException(
                "Nie mozna recznie dodawac pozycji do palety " +
                "utworzonej z zatwierdzonego przyjecia.");
        }

        // 4. Walidacja ilosci
        if (request.Quantity <= 0 ||
            request.Quantity > 999999999999999.999m ||
            decimal.Round(request.Quantity, 3) != request.Quantity)
        {
            throw new InvalidLogisticUnitItemException(
                "Ilosc musi byc dodatnia i miec maksymalnie " +
                "3 miejsca po przecinku.");
        }

        // 5. Sprawdzenie produktu
        if (!await _repository.ProductIsActiveAsync(
            request.ProductId, cancellationToken))
        {
            throw new InvalidLogisticUnitItemException(
                "Produkt nie istnieje lub jest nieaktywny.");
        }

        // 6. Sprawdzenie partii produktu (jesli podana)
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

        // 7. Sprawdzenie duplikatu pozycji
        if (await _repository.ItemExistsAsync(
            logisticUnitId,
            request.ProductId,
            request.ProductBatchId,
            cancellationToken))
        {
            throw new DuplicateLogisticUnitItemException();
        }

        // 8. Przygotowanie nowej pozycji palety
        var item = new LogisticUnitItem
        {
            LogisticUnitId = logisticUnitId,
            ProductId = request.ProductId,
            ProductBatchId = request.ProductBatchId,
            Quantity = request.Quantity
        };

        // 9. Zapis do bazy danych
        await _repository.AddAsync(
            item, cancellationToken);

        await _repository.SaveChangesAsync(
            cancellationToken);

        // 10. Ponowny odczyt zapisanej pozycji
        var created = await _repository.GetByIdAsync(
            logisticUnitId,
            item.Id,
            cancellationToken);

        return MapToDto(created
            ?? throw new InvalidOperationException(
                "Created logistic unit item could not be loaded."));
    }

    // Mapowanie encji na DTO
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

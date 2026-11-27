
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.DTOs.Stock;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;
using Warehouse.Infrastructure.Persistence;

namespace Warehouse.Infrastructure.Repositories;

public class StockAvailabilityRepository
    : IStockAvailabilityRepository
{
    private readonly WarehouseDbContext _context;

    public StockAvailabilityRepository(
        WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task<(
        IReadOnlyList<StockAvailabilityDto> Items,
        int TotalCount)> GetPageAsync(
        StockAvailabilityQuery filter,
        CancellationToken cancellationToken)
    {
        // 1. Fizyczny stan magazynowy.
        IQueryable<LogisticUnitItem> stockQuery =
            _context.LogisticUnitItems
                .AsNoTracking()
                .Where(x =>
                    x.Quantity > 0m &&
                    x.LogisticUnit.Status ==
                        LogisticUnitStatus.Stored &&
                    x.LogisticUnit.StorageLocation != null &&
                    x.LogisticUnit.StorageLocation.IsActive &&
                    x.LogisticUnit.StorageLocation
                        .WarehouseZone.IsActive &&
                    x.LogisticUnit.StorageLocation
                        .WarehouseZone.Warehouse.IsActive);

        // 2. Niezrealizowane pozycje zamówień Released.
        IQueryable<WarehouseOrderItem> orderQuery =
            _context.WarehouseOrderItems
                .AsNoTracking()
                .Where(x =>
                    x.WarehouseOrder.Status ==
                        WarehouseOrderStatus.Released &&
                    x.WarehouseOrder.Warehouse.IsActive);

        if (filter.WarehouseId.HasValue)
        {
            var warehouseId = filter.WarehouseId.Value;

            stockQuery = stockQuery.Where(x =>
                x.LogisticUnit.StorageLocation!
                    .WarehouseZone.WarehouseId == warehouseId);

            orderQuery = orderQuery.Where(x =>
                x.WarehouseOrder.WarehouseId == warehouseId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();

            stockQuery = stockQuery.Where(x =>
                x.Product.Code.Contains(search) ||
                x.Product.Name.Contains(search));

            orderQuery = orderQuery.Where(x =>
                x.Product.Code.Contains(search) ||
                x.Product.Name.Contains(search));
        }

        // 3. SQL Server sumuje stan fizyczny
        // dla magazynu i produktu.
        var physical = await stockQuery
            .GroupBy(x => new
            {
                WarehouseId = x.LogisticUnit.StorageLocation!
                    .WarehouseZone.WarehouseId,

                WarehouseCode = x.LogisticUnit.StorageLocation!
                    .WarehouseZone.Warehouse.Code,

                x.ProductId,
                ProductCode = x.Product.Code,
                ProductName = x.Product.Name,
                UnitSymbol = x.Product.UnitOfMeasure.Symbol
            })
            .Select(group => new
            {
                group.Key.WarehouseId,
                group.Key.WarehouseCode,
                group.Key.ProductId,
                group.Key.ProductCode,
                group.Key.ProductName,
                group.Key.UnitSymbol,
                Quantity = group.Sum(x => x.Quantity)
            })
            .ToListAsync(cancellationToken);

        // 4. Pobrania liczymy z historii kompletacji.
        // To ta sama zasada, której używa
        // WarehouseOrderRepository przy zwalnianiu zamówień.
        var outstandingLines = await orderQuery
            .Select(x => new
            {
                x.WarehouseOrder.WarehouseId,
                WarehouseCode = x.WarehouseOrder.Warehouse.Code,
                x.ProductId,
                ProductCode = x.Product.Code,
                ProductName = x.Product.Name,
                UnitSymbol = x.Product.UnitOfMeasure.Symbol,

                x.QuantityRequested,

                QuantityPicked = _context.PickingOperations
                    .Where(p => p.WarehouseOrderItemId == x.Id)
                    .Sum(p => (decimal?)p.Quantity) ?? 0m
            })
            .ToListAsync(cancellationToken);

        // 5. Łączymy wyniki po WarehouseId + ProductId.
        // Nie przypisujemy rezerwacji do konkretnej palety.
        var balances =
            new Dictionary<(int WarehouseId, int ProductId),
                StockAvailabilityDto>();

        foreach (var item in physical)
        {
            var key = (item.WarehouseId, item.ProductId);

            balances[key] = new StockAvailabilityDto(
                item.WarehouseId,
                item.WarehouseCode,
                item.ProductId,
                item.ProductCode,
                item.ProductName,
                item.UnitSymbol,
                item.Quantity,
                0m,
                item.Quantity);
        }

        foreach (var item in outstandingLines)
        {
            // Zamówienie może zostać częściowo skompletowane.
            // Już pobrana ilość nie stanowi rezerwacji.
            var remaining = Math.Max(
                0m,
                item.QuantityRequested - item.QuantityPicked);

            if (remaining == 0m)
                continue;

            var key = (item.WarehouseId, item.ProductId);

            if (balances.TryGetValue(key, out var current))
            {
                balances[key] = current with
                {
                    OutstandingQuantity =
                        current.OutstandingQuantity + remaining,

                    FreeQuantity =
                        current.FreeQuantity - remaining
                };
            }
            else
            {
                // Pokazujemy również niedobór:
                // istnieje zapotrzebowanie, ale brak stanu.
                balances[key] = new StockAvailabilityDto(
                    item.WarehouseId,
                    item.WarehouseCode,
                    item.ProductId,
                    item.ProductCode,
                    item.ProductName,
                    item.UnitSymbol,
                    0m,
                    remaining,
                    -remaining);
            }
        }

        // 6. Sortowanie i paginacja po agregacji.
        var rows = balances.Values
            .OrderBy(x => x.WarehouseCode)
            .ThenBy(x => x.ProductCode)
            .ThenBy(x => x.WarehouseId)
            .ThenBy(x => x.ProductId)
            .ToList();

        var totalCount = rows.Count;

        var pageItems = rows
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        return (pageItems, totalCount);
    }
}
